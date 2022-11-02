using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public sealed class Rider
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("order_timestamp")]
    public long OrderTimestamp { get; set; }
    
    [JsonPropertyName("invoice_link")]
    public string? InvoiceLink { get; set; }
    
    [JsonPropertyName("price_with_vat_str")]
    public string? PriceWithVatStr { get; set; }

    [JsonPropertyName("stops")]
    public string[] Stops { get; set; } = Array.Empty<string>();
}