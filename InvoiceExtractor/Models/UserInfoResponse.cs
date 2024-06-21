using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public sealed class UserInfo
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("is_verification_needed")]
    public bool IsVerificationNeeded { get; set; }
    
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public sealed class UserInfoResponse : BoltResponse<UserInfo>
{
    
}