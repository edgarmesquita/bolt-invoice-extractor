using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public class AuthenticationConfirmationRequest
{
    [JsonPropertyName("verification_token")]
    public string? VerificationToken { get; set; }
    
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [JsonPropertyName("device_uid")]
    public string? DeviceUid { get; set; }
    
    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }
    
    [JsonPropertyName("device_os_version")]
    public string? DeviceOsVersion { get; set; }
}