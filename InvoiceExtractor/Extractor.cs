using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bolt.Business.InvoiceExtractor.Models;

namespace Bolt.Business.InvoiceExtractor;

public class Extractor : IDisposable
{
    private readonly BusinessPortalClient _client;
    //594dcd56-0f8e-4a89-b0d4-b6f406cd89c0b1667597933
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

    private async Task<string?> GetAccessTokenAsync()
    {
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
        

        string? refreshToken;
        try
        {
            var refreshTokenData = await _client.CompleteAuthenticationAsync(sms, verificationToken);
            refreshToken = refreshTokenData.RefreshToken;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return null;
        }

        string? accessToken;
        try
        {
            var accessTokenData = await _client.GetAccessTokenAsync(refreshToken);
            accessToken = accessTokenData.AccessToken;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return null;
        }

        return accessToken;
    }
    
    public async Task ExtractAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken)) return;
        
        _client.UpdateSessionId(accessToken);
        
        int userId;
        try
        {
            var userInfo = await _client.GetUserInfoAsync(accessToken);
            userId = userInfo.Id;
        }
        catch (Exception ex)
        {
            DisplayError(ex);
            return;
        }

        var list = await GetAMonthOfRidersAsync(accessToken, userId);

        foreach (var rider in list)
        {
            await _client.DownloadFileAsync(rider.InvoiceLink!, $"{rider.Id}.pdf");
        }
    }

    private async Task<IEnumerable<Ride>> GetAMonthOfRidersAsync(string accessToken, int userId)
    {
        var year = ReadInteger("Type an year", "Invalid year. Assuming current year") ?? DateTime.Now.Year;
        var month = ReadInteger("Type a month (integer between 1 and 12)", "Invalid month. Assuming current month") ?? DateTime.Now.Month;
        var startDate = new DateTime(year, month, 1);
        var endDate = new DateTime(year, month, 1).AddMonths(1).AddTicks(-1);

        var list = new List<Ride>();
        (IEnumerable<Ride> List, int TotalPages) currentPage;
        try
        {
            currentPage = await GetFilteredRiders(accessToken, userId, startDate, endDate, 1);
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
                var filteredPage = await GetFilteredRiders(accessToken, userId, startDate, endDate, page);
                if(!filteredPage.List.Any())
                    break;
                
                list.AddRange(filteredPage.List);
            }
        }

        return list;
    }

    private async Task<(IEnumerable<Ride> List, int TotalPages)> GetFilteredRiders(string accessToken, int userId, DateTime startDate, DateTime endDate, int page)
    {
        var currentPage = await _client.GetRiderPageAsync(accessToken, userId, page);
        return (currentPage.List
            .Where(rider => !string.IsNullOrEmpty(rider.InvoiceLink) && rider.OrderTimestamp >= startDate && rider.OrderTimestamp <= endDate)
            .ToList(), currentPage.Pagination.TotalPages);
    }

    private static void DisplayError(Exception ex)
    {
        Console.WriteLine(ex.Message);
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