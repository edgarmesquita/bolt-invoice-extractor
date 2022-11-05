using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bolt.Business.InvoiceExtractor.Models;

namespace Bolt.Business.InvoiceExtractor;

public class Extractor : IDisposable
{
    private readonly BusinessPortalClient _client;
    private const string TokenInfoFilename = "TokenInfo.json";

    public Extractor()
    {
        _client = new BusinessPortalClient();
    }

    private static LoginRequest? GetLoginRequest()
    {
        var loginRequest = new LoginRequest
        {
            Username = ReadString("Type your username:", "Invalid Username")
        };

        if (string.IsNullOrEmpty(loginRequest.Username))
            return null;
        
        
        loginRequest.Password = ReadLineMasked("Type your password:", "Invalid Password");

        return string.IsNullOrEmpty(loginRequest.Password) ? null : loginRequest;
    }

    private async Task<TokenInfo?> GetTokenInfoAsync()
    {
        var result = new TokenInfo();
        
        var loginRequest = GetLoginRequest();
        if (loginRequest == null) return null;

        string? verificationToken;
        try
        {
            var authenticationToken = await _client.StartAuthenticationAsync(loginRequest);
            verificationToken = authenticationToken.VerificationToken;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return null;
        }

        var sms = ReadString("Type sms code:", "Invalid SMS Code");

        if (string.IsNullOrEmpty(sms))
            return null;
        
        try
        {
            var refreshTokenData = await _client.CompleteAuthenticationAsync(sms, verificationToken);
            result.RefreshToken = refreshTokenData.RefreshToken;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return null;
        }

        result = await UpdateAccessTokenAsync(result);
        return result;
    }

    private async Task<TokenInfo?> UpdateAccessTokenAsync(TokenInfo? tokenInfo)
    {
        if (tokenInfo == null) return null;
        
        try
        {
            var accessTokenData = await _client.GetAccessTokenAsync(tokenInfo.RefreshToken);
            tokenInfo.AccessToken = accessTokenData.AccessToken;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return null;
        }

        return tokenInfo;
    }

    private static async Task WriteTokenInfoFileAsync(TokenInfo tokenInfo)
    {
        var tokenInfoJson = JsonSerializer.Serialize(tokenInfo);
        await File.WriteAllTextAsync(TokenInfoFilename, tokenInfoJson);
    }
    private async Task<TokenInfo?> GetCachedTokenInfoAsync()
    {
        if (!File.Exists(TokenInfoFilename))
        {
            var tokenInfo = await GetTokenInfoAsync();
            if(tokenInfo == null) return null;

            await WriteTokenInfoFileAsync(tokenInfo);

            return tokenInfo;
        }
        
        await using var stream = File.OpenRead(TokenInfoFilename);
        return await JsonSerializer.DeserializeAsync<TokenInfo>(stream);
    }

    private async Task<TokenInfo?> UpdateCachedTokenInfoAsync(TokenInfo? tokenInfo)
    {
        tokenInfo = await UpdateAccessTokenAsync(tokenInfo);
        if(tokenInfo == null || string.IsNullOrEmpty(tokenInfo.AccessToken)) return null;

        await WriteTokenInfoFileAsync(tokenInfo);
        
        _client.UpdateSessionId(tokenInfo.AccessToken);
        
        return tokenInfo;
    }
    
    public async Task ExtractAsync()
    {
        var tokenInfo = await GetCachedTokenInfoAsync();
        if(tokenInfo == null) return;
        
        if (string.IsNullOrEmpty(tokenInfo.AccessToken)) return;
        
        _client.UpdateSessionId(tokenInfo.AccessToken);
        
        int userId;
        try
        {
            var userInfo = await _client.GetUserInfoAsync(tokenInfo.AccessToken);
            userId = userInfo.Id;
        }
        catch
        {
            await UpdateCachedTokenInfoAsync(tokenInfo);

            try
            {
                var userInfo = await _client.GetUserInfoAsync(tokenInfo.AccessToken);
                userId = userInfo.Id;
            }
            catch (Exception ex)
            {
                DisplayError(ex);
                return;
            }
        }

        int companyId;
        try
        {
            var companyListData = await _client.GetAssociatedCompaniesForUser(tokenInfo.AccessToken);
            companyId = companyListData.Companies.FirstOrDefault()?.Id ?? 0;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return;
        }

        var year = ReadInteger("Type an year", "Invalid year. Assuming current year") ?? DateTime.Now.Year;
        var month = ReadInteger("Type a month (integer between 1 and 12)", "Invalid month. Assuming current month") ?? DateTime.Now.Month;
        var list = await GetAMonthOfRidersAsync(tokenInfo.AccessToken, companyId, year, month);

        foreach (var ride in list)
        {
            Console.WriteLine($"Downloading ride {ride.Id} ({ride.OrderTimestamp})");
            await _client.DownloadFileAsync(ride.InvoiceLink!, $"{ride.Id}.pdf", $"{year}-{month.ToString().PadLeft(2, '0')}");
        }
        
        Console.WriteLine();
        Console.WriteLine("All files downloaded!");
        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }

    private async Task<IEnumerable<Ride>> GetAMonthOfRidersAsync(string accessToken, int companyId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = new DateTime(year, month, 1).AddMonths(1).AddTicks(-1);

        var list = new List<Ride>();
        (IEnumerable<Ride> List, int TotalPages) currentPage;
        try
        {
            currentPage = await GetFilteredRiders(accessToken, companyId, startDate, endDate, 1);
            list.AddRange(currentPage.List);
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return Enumerable.Empty<Ride>();
        }

        if (currentPage.TotalPages > 1 && list.Any())
        {
            var pages = Enumerable.Range(2, currentPage.TotalPages - 1);
            foreach (var page in pages)
            {
                var filteredPage = await GetFilteredRiders(accessToken, companyId, startDate, endDate, page);
                if(!filteredPage.List.Any())
                    break;
                
                list.AddRange(filteredPage.List);
            }
        }

        return list;
    }

    private async Task<(IEnumerable<Ride> List, int TotalPages)> GetFilteredRiders(string accessToken, int companyId, DateTime startDate, DateTime endDate, int page)
    {
        var currentPage = await _client.GetRiderPageAsync(accessToken, companyId, page);
        return (currentPage.List
            .Where(rider => !string.IsNullOrEmpty(rider.InvoiceLink) && rider.OrderTimestamp >= startDate && rider.OrderTimestamp <= endDate)
            .ToList(), currentPage.Pagination.TotalPages);
    }

    private static void DisplayError(Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine(ex.Message);
        
        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }

    private static string? ReadString(string label, string errorMessage)
    {
        Console.WriteLine(label);
        
        var text = Console.ReadLine();

        if (!string.IsNullOrEmpty(text)) 
            return text;
        
        Console.WriteLine(errorMessage);
        Console.ReadLine();
        return null;
    }

    private static int? ReadInteger(string label, string errorMessage)
    {
        var value = ReadString(label, errorMessage);

        if (int.TryParse(value, out var result))
            return result;
        
        Console.WriteLine(errorMessage);
        Console.ReadLine();
        return null;
    }
    private static string? ReadLineMasked(string label, string errorMessage, char mask = '*')
    {
        Console.WriteLine(label);
        
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
        var result = sb.ToString();
        if (!string.IsNullOrEmpty(result))
            return result;
        
        Console.WriteLine(errorMessage);
        Console.ReadLine();
        return null;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}