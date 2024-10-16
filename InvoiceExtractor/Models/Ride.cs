using System.Text.Json.Serialization;
using Bolt.Business.InvoiceExtractor.Converters;

namespace Bolt.Business.InvoiceExtractor.Models;

public sealed class Ride
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("order_timestamp")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime OrderTimestamp { get; set; }
    
    [JsonPropertyName("price_with_vat_str")]
    public string? PriceWithVatStr { get; set; }

    [JsonPropertyName("stops")]
    public string[] Stops { get; set; } = Array.Empty<string>();

    [JsonPropertyName("user_invoices")]
    public List<UserInvoice> UserInvoices { get; set; } = [];
}