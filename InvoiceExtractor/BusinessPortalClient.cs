using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bolt.Business.InvoiceExtractor.Extensions;
using Bolt.Business.InvoiceExtractor.Models;
using Bolt.Business.InvoiceExtractor.Models.Auth;

namespace Bolt.Business.InvoiceExtractor;

public class BusinessPortalClient : IDisposable
{
    private readonly HttpClient _client;
    private string _sessionId;
    private readonly Guid _deviceUid;
    private const string Version = "BP.11.61";
    
    public BusinessPortalClient()
    {
        _client = new HttpClient();
        var startTimestamp = DateTime.UtcNow.ToUnixTimeInSeconds();
        _sessionId = Guid.NewGuid() + "b" + startTimestamp;
        _deviceUid = Guid.NewGuid(); //22985b26-5113-4f1f-a32d-9c11bbf1ef4c
    }

    public async Task<AuthenticationToken> StartAuthenticationAsync(LoginRequest loginRequest)
    {
        var response = await _client.PostAsync(
            $"https://node.bolt.eu/business-portal/businessPortal/startAuthentication?version={Version}&session_id={_sessionId}",
            JsonContent.Create(new AuthenticationRequest
            {
                DeviceUid = _deviceUid.ToString(),
                DeviceName = "Chrome (Windows)",
                DeviceOsVersion = "106.0.0.0",
                Username = loginRequest.Username,
                Password = loginRequest.Password
            }));
        var content = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when starting authentication");
    }

    public async Task<RefreshTokenData> CompleteAuthenticationAsync(string sms, string verificationToken)
    {
        var confirmationResponse = await _client.PostAsync(
            $"https://node.bolt.eu/business-portal/businessPortal/completeAuthentication?version={Version}&session_id={_sessionId}",
            JsonContent.Create(new AuthenticationConfirmationRequest
            {
                DeviceUid = _deviceUid.ToString(),
                DeviceName = "Chrome (Windows)",
                DeviceOsVersion = "106.0.0.0",
                Code = sms,
                VerificationToken = verificationToken
            }));

        var content = await confirmationResponse.Content.ReadFromJsonAsync<AuthenticationConfirmationResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when completing authentication");
    }

    public async Task<AccessTokenData> GetAccessTokenAsync(string refreshToken)
    {
        var accessTokenResponse = await _client.PostAsync(
            $"https://node.bolt.eu/business-portal/businessPortal/getAccessToken?version={Version}&session_id={_sessionId}",
            JsonContent.Create(new RefreshTokenData
            {
                RefreshToken = refreshToken
            }));

        var content = await accessTokenResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when getting access token");
    }

    public async Task<UserInfo> GetUserInfoAsync(string accessToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get,
            $"https://node.bolt.eu/business-portal/businessPortalUser/getUserInfo/?version={Version}&session_id={_sessionId}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userResponse = await _client.SendAsync(requestMessage);
        var content = await userResponse.Content.ReadFromJsonAsync<UserInfoResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when getting user info");
    }

    public async Task<CompanyListData> GetAssociatedCompaniesForUser(string accessToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get,
            $"https://node.bolt.eu/business-portal/businessPortalUser/getAssociatedCompaniesForUser/?version={Version}&session_id={_sessionId}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(requestMessage);
        var content = await response.Content.ReadFromJsonAsync<CompanyListResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when getting associated companies for user");
    }
    
    public async Task<RideListData> GetRidePageAsync(
        string accessToken, int companyId, 
        int? year = null, int? month = null, 
        int page = 1, int limit = 100)
    {
        var url = $"https://node.bolt.eu/business-portal/businessPortal/getRidesHistory/?version={Version}&session_id={_sessionId}&company_id={companyId}&limit={limit}&page={page}";
        if (year.HasValue) url += $"&year={year}";
        if (month.HasValue) url += $"&month={month}";
        
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var listResponse = await _client.SendAsync(requestMessage);
        var content = await listResponse.Content.ReadFromJsonAsync<RideListResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when getting riders");
    }
    
    public async Task DownloadFileAsync(string url, string filename, string folder, IProgress<double>? progress = null)
    {
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        
        // var response = await _client.GetAsync(url);
        // await using var fileStream = new FileStream(Path.Combine(folder, filename), FileMode.CreateNew);
        // await response.Content.CopyToAsync(fileStream);

        using var response = _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        
        await using Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream(Path.Combine(folder, filename), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        var totalRead = 0L;
        var totalReads = 0L;
        var buffer = new byte[8192];
        var isMoreToRead = true;

        do
        {
            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
            {
                isMoreToRead = false;
            }
            else
            {
                await fileStream.WriteAsync(buffer, 0, read);

                totalRead += read;
                totalReads += 1;

                if (totalReads % 2000 == 0)
                {
                    //Console.WriteLine(string.Format("total bytes downloaded so far: {0:n0}", totalRead));
                    progress?.Report((totalRead * 1d) / (total * 1d) * 100);
                }
            }
        }
        while (isMoreToRead);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
    
    public void UpdateSessionId(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(accessToken);

        var iatClaim = jwtSecurityToken.Claims
            .FirstOrDefault(claim => claim.Type == "iat")?.Value;
        
        var dataClaimJson = jwtSecurityToken.Claims
            .FirstOrDefault(claim => claim.Type == "data")?.Value;

        if (string.IsNullOrEmpty(dataClaimJson))
            return;

        var dataClaim = JsonSerializer.Deserialize<DataClaim>(dataClaimJson);
        
        if(dataClaim?.BusinessAdminUserId > 0 && !string.IsNullOrEmpty(iatClaim))
            _sessionId = $"{dataClaim.BusinessAdminUserId}b{iatClaim}";
    }
}