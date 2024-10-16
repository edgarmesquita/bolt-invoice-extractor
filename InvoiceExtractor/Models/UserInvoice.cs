using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public class UserInvoice
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("public_id")]
    public string? PublicId { get; set; }
    
    [JsonPropertyName("link")]
    public string? InvoiceLink { get; set; }
}