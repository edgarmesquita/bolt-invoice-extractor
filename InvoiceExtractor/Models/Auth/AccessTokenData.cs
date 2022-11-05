using System.Text.Json.Serialization;
using Bolt.Business.InvoiceExtractor.Converters;

namespace Bolt.Business.InvoiceExtractor.Models.Auth;

public sealed class AccessTokenData
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("expires_in_seconds")]
    public int ExpiresInSeconds { get; set; }
    
    [JsonPropertyName("expires_timestamp")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime ExpiresTimestamp { get; set; }
    
    [JsonPropertyName("next_update_give_up_timestamp")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime NextUpdateGiveUpTimestamp { get; set; }
    
    [JsonPropertyName("next_update_in_seconds")]
    public int NextUpdateInSeconds { get; set; }
}