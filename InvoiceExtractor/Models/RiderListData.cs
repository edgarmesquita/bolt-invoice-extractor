using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public sealed class RiderListData
{
    [JsonPropertyName("list")]
    public List<Rider> List { get; set; } = new();

    [JsonPropertyName("pagination")]
    public BoltPagination Pagination { get; set; } = new();
}