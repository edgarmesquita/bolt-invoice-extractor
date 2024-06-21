using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public sealed class RideListData
{
    [JsonPropertyName("list")]
    public List<Ride> List { get; set; } = new();

    [JsonPropertyName("pagination")]
    public BoltPagination Pagination { get; set; } = new();
}