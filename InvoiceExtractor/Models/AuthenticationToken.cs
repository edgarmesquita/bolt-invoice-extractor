using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public class AuthenticationToken
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("verification_token")]
    public string VerificationToken { get; set; }
    
    [JsonPropertyName("verification_code_channel")]
    public string? VerificationCodeChannel { get; set; }
    
    [JsonPropertyName("verification_code_target")]
    public string? VerificationCodeTarget { get; set; }
    
    [JsonPropertyName("verification_code_length")]
    public int VerificationCodeLength { get; set; }
    
    [JsonPropertyName("resend_wait_time_seconds")]
    public int ResendWaitTimeSeconds { get; set; }

    [JsonPropertyName("available_verification_channels")]
    public string[] AvailableVerificationChannels { get; set; } = Array.Empty<string>();
}