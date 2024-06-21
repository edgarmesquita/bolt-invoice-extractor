using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models.Auth;

public class AuthenticationRequest
{
    [JsonPropertyName("device_uid")]
    public string? DeviceUid { get; set; }
    
    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }
    
    [JsonPropertyName("device_os_version")]
    public string? DeviceOsVersion { get; set; }
    
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    
    [JsonPropertyName("password")]
    public string? Password { get; set; }
}