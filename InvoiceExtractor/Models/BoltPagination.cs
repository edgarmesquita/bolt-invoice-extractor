using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public class BoltPagination {
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }
    
    [JsonPropertyName("items_per_page")]
    public int ItemsPerPage { get; set; }
    
    [JsonPropertyName("total_items")]
    public int TotalItems { get; set; }
    
    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}