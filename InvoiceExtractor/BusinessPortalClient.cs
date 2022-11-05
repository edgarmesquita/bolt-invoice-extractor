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

    public async Task<RideListData> GetRiderPageAsync(string accessToken, int companyId, int page, int limit = 100)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get,
            $"https://node.bolt.eu/business-portal/businessPortal/getRidesHistory/?version={Version}&session_id={_sessionId}&company_id={companyId}&limit={limit}&page={page}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var listResponse = await _client.SendAsync(requestMessage);
        var content = await listResponse.Content.ReadFromJsonAsync<RideListResponse>();

        return content is { Message: "OK", Data: { } }
            ? content.Data
            : throw new Exception(content?.Message ?? "Bad Request when getting riders");
    }
    
    public async Task DownloadFileAsync(string url, string filename)
    {
        var response = await _client.GetAsync(url);
        await using var fileStream = new FileStream(filename, FileMode.CreateNew);
        await response.Content.CopyToAsync(fileStream);
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