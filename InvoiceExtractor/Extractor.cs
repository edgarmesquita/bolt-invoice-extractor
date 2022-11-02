using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bolt.Business.InvoiceExtractor.Models;

namespace Bolt.Business.InvoiceExtractor;

public class Extractor : IDisposable
{
    private readonly HttpClient _client;
    private readonly Guid _sessionId;

    public Extractor()
    {
        _client = new HttpClient();
        _sessionId = Guid.NewGuid();
    }

    private async Task<AuthenticationToken> StartAuthentication(string username, string password)
    {
        var response = await _client.PostAsync(
            $"https://node.bolt.eu/business-portal/businessPortal/startAuthentication?version=BP.11.58&session_id={_sessionId}",
            JsonContent.Create(new AuthenticationRequest
            {
                DeviceUid = "22985b26-5113-4f1f-a32d-9c11bbf1ef4c",
                DeviceName = "Chrome (Windows)",
                DeviceOsVersion = "106.0.0.0",
                Username = username,
                Password = password
            }));
        var content = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        return content is { Message: "OK", Data: { } } ? content.Data : throw new Exception(content?.Message ?? "Bad Request when starting authentication");
    }

    private async Task<RefreshTokenData> CompleteAuthentication(string sms, string verificationToken)
    {
        var confirmationResponse = await _client.PostAsync(
            $"https://node.bolt.eu/business-portal/businessPortal/completeAuthentication?version=BP.11.58&session_id={_sessionId}",
            JsonContent.Create(new AuthenticationConfirmationRequest
            {
                DeviceUid = "22985b26-5113-4f1f-a32d-9c11bbf1ef4c",
                DeviceName = "Chrome (Windows)",
                DeviceOsVersion = "106.0.0.0",
                Code = sms,
                VerificationToken = verificationToken
            }));

        var content = await confirmationResponse.Content.ReadFromJsonAsync<AuthenticationConfirmationResponse>();

        return content is { Message: "OK", Data: { } } ? content.Data : throw new Exception(content?.Message ?? "Bad Request when completing authentication");
    }

    private async Task<AccessTokenData> GetAccessTokenAsync(string refreshToken)
    {
        var accessTokenResponse = await _client.PostAsync(
            $"https://node.bolt.eu/business-portal/businessPortal/getAccessToken?version=BP.11.58&session_id={_sessionId}",
            JsonContent.Create(new RefreshTokenData
            {
                RefreshToken = refreshToken
            }));

        var content = await accessTokenResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();

        return content is  { Message: "OK", Data: { } } ?  content.Data : throw new Exception(content?.Message ?? "Bad Request when getting access token");
    }

    private async Task<UserInfo> GetUserInfoAsync(string accessToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get,
            $"https://node.bolt.eu/business-portal/businessPortalUser/getUserInfo/?version=BP.11.58&session_id={_sessionId}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        var userResponse = await _client.SendAsync(requestMessage);
        var content = await userResponse.Content.ReadFromJsonAsync<UserInfoResponse>();
        
        return content is { Message: "OK", Data: { } } ? content.Data : throw new Exception(content?.Message ?? "Bad Request when getting user info");
    }

    private async Task<RiderListData> GetRiderPage(string accessToken, int companyId, int page, int limit = 100)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get,
            $"https://node.bolt.eu/business-portal/businessPortal/getRidesHistory/?version=BP.11.58&session_id={_sessionId}&company_id={companyId}&limit={limit}&page={page}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        var listResponse = await _client.SendAsync(requestMessage);
        var content = await listResponse.Content.ReadFromJsonAsync<RiderListResponse>();

        return content is { Message: "OK", Data: { } } ? content.Data : throw new Exception(content?.Message ?? "Bad Request when getting riders");
    }
    public async Task ExtractAsync()
    {
        Console.WriteLine("Type your username:");
        var username = Console.ReadLine();

        if (string.IsNullOrEmpty(username))
        {
            Console.WriteLine("Invalid Username");
            Console.ReadLine();
            return;
        }

        Console.WriteLine("Type your password:");
        var password = ReadLineMasked();
        
        if (string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Invalid Password");
            Console.ReadLine();
            return;
        }

        string? verificationToken;
        try
        {
            var authenticationToken = await StartAuthentication(username, password);
            verificationToken = authenticationToken.VerificationToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.ReadLine();
            return;
        }

        Console.WriteLine("Type sms code:");
        var sms = Console.ReadLine();

        if (string.IsNullOrEmpty(sms))
        {
            Console.WriteLine("Invalid SMS Code");
            Console.ReadLine();
            return;
        }

        string? refreshToken;
        try
        {
            var refreshTokenData = await CompleteAuthentication(sms, verificationToken);
            refreshToken = refreshTokenData.RefreshToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.ReadLine();
            return;
        }

        string? accessToken;
        try
        {
            var accessTokenData = await GetAccessTokenAsync(refreshToken);
            accessToken = accessTokenData.AccessToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.ReadLine();
            return;
        }

        int userId;
        try
        {
            var userInfo = await GetUserInfoAsync(accessToken!);
            userId = userInfo.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.ReadLine();
            return;
        }

        Console.WriteLine("Reading Page 1");

        RiderListData page1;
        try
        {
            page1 = await GetRiderPage(accessToken!, userId, 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.ReadLine();
            return;
        }
        
        foreach (var rider in page1.List.Where(rider => !string.IsNullOrEmpty(rider.InvoiceLink)))
        {
            await DownloadFileAsync(rider.InvoiceLink!, $"{rider.Id}.pdf");
        }
    }

    private async Task DownloadFileAsync(string url, string filename)
    {
        var response = await _client.GetAsync(url);
        await using var fileStream = new FileStream(filename, FileMode.CreateNew);
        await response.Content.CopyToAsync(fileStream);
    }

    private static string ReadLineMasked(char mask = '*')
    {
        var sb = new StringBuilder();
        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (!char.IsControl(keyInfo.KeyChar))
            {
                sb.Append(keyInfo.KeyChar);
                Console.Write(mask);
            }
            else if (keyInfo.Key == ConsoleKey.Backspace && sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);

                if (Console.CursorLeft == 0)
                {
                    Console.SetCursorPosition(Console.BufferWidth - 1, Console.CursorTop - 1);
                    Console.Write(' ');
                    Console.SetCursorPosition(Console.BufferWidth - 1, Console.CursorTop - 1);
                }
                else Console.Write("\b \b");
            }
        }
        Console.WriteLine();
        return sb.ToString();
    }
    
    public void Dispose()
    {
        _client.Dispose();
    }
}