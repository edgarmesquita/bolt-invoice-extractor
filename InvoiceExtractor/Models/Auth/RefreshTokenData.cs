using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models.Auth;

public class RefreshTokenData
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }
}