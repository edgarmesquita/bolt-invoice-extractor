using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models.Auth;

public sealed class AccessTokenData
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
}