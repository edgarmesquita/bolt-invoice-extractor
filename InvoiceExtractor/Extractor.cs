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
        
        UserInfo userInfo;
        try
        {
            userInfo = await _client.GetUserInfoAsync(tokenInfo.AccessToken);
        }
        catch
        {
            await UpdateCachedTokenInfoAsync(tokenInfo);

            try
            {
                userInfo = await _client.GetUserInfoAsync(tokenInfo.AccessToken);
            }
            catch (Exception ex)
            {
                DisplayError(ex);
                return;
            }
        }

        Console.WriteLine($"User: {userInfo.FirstName} {userInfo.LastName}");
        Console.WriteLine($"E-mail: {userInfo.Email}");
        
        CompanyInfo? company;
        try
        {
            var companyListData = await _client.GetAssociatedCompaniesForUser(tokenInfo.AccessToken);
            company = companyListData.Companies.FirstOrDefault();
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return;
        }
        if(company == null) return;
        
        Console.WriteLine($"Company: {company.Name}");
        Console.WriteLine();
        
        var year = ReadInteger("Type an year", "Invalid year. Assuming current year") ?? DateTime.Now.Year;
        var month = ReadInteger("Type a month (integer between 1 and 12)", "Invalid month. Assuming current month") ?? DateTime.Now.Month;
        var list = await GetAMonthOfRidesAsync(tokenInfo.AccessToken, company.Id, year, month);
        var count = list.Count;

        if (count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("No invoices found.");
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
            return;
        }

        var folder = GetFolderName(year, month);
        
        foreach (var ride in list)
        {
            Console.WriteLine($"Downloading ride {ride.Id} ({ride.OrderTimestamp})");
            using var progress = new ProgressBar();
            await _client.DownloadFileAsync(
            ride.InvoiceLink!, 
            $"{ride.OrderTimestamp:yyyy-MM-dd-HH-mm-ss}.pdf", 
            folder, 
            progress);
        }
        
        Console.WriteLine();
        Console.WriteLine($"{count} invoices downloaded!");
        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }

    private static string GetFolderName(int year, int month)
    {
        var path = ReadString("Type the output folder path (Assuming local folder when empty)", "Assuming local folder...", false);
        var folder = $"{year}-{month.ToString().PadLeft(2, '0')}";

        if (string.IsNullOrEmpty(path)) 
            return folder;
        
        if (Directory.Exists(path))
            folder = Path.Combine(path, folder);
        else
        {
            Console.WriteLine("Invalid path. Assuming local folder.");
            Console.WriteLine();
        }

        return folder;
    }

    private async Task<List<Ride>> GetAMonthOfRidesAsync(string accessToken, int companyId, int year, int month)
    {
        var list = new List<Ride>();
        (IEnumerable<Ride> List, int TotalPages) currentPage;
        try
        {
            currentPage = await GetFilteredRides(accessToken, companyId, year, month, 1);
            list.AddRange(currentPage.List);
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return new List<Ride>();
        }

        if (currentPage.TotalPages > 1 && list.Any())
        {
            var pages = Enumerable.Range(2, currentPage.TotalPages - 1);
            foreach (var page in pages)
            {
                var filteredPage = await GetFilteredRides(accessToken, companyId, year, month, page);
                if(!filteredPage.List.Any())
                    break;
                
                list.AddRange(filteredPage.List);
            }
        }

        return list;
    }

    private async Task<(IEnumerable<Ride> List, int TotalPages)> GetFilteredRides(string accessToken, int companyId, int year, int month, int page)
    {
        var currentPage = await _client.GetRidePageAsync(accessToken, companyId, year, month, page);
        return (currentPage.List
            .Where(rider => !string.IsNullOrEmpty(rider.InvoiceLink))
            .ToList(), currentPage.Pagination.TotalPages);
    }

    private static void DisplayError(Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine(ex.Message);
        
        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }

    private static string? ReadString(string label, string errorMessage, bool waitingConfirmation = true)
    {
        Console.WriteLine(label);
        
        var text = Console.ReadLine();

        if (!string.IsNullOrEmpty(text)) 
            return text;
        
        Console.WriteLine(errorMessage);
        
        if(waitingConfirmation)
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