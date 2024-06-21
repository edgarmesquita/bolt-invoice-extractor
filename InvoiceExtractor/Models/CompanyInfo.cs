using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models;

public class CompanyInfo
{
    [JsonPropertyName("company_id")]
    public int Id { get; set; }
    
    [JsonPropertyName("company_name")]
    public string Name { get; set; }
    
    [JsonPropertyName("is_selected")]
    public bool IsSelected { get; set; }
}