using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public class CompanyListData
{
    [JsonPropertyName("companies")]
    public List<CompanyInfo> Companies { get; set; } = new();
}