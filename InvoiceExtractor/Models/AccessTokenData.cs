using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public sealed class AccessTokenData
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
}