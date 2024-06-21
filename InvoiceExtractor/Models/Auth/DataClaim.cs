using System.Text.Json.Serialization;

namespace Bolt.Business.InvoiceExtractor.Models.Auth;

public class DataClaim
{
    [JsonPropertyName("business_admin_user_id")]
    public int BusinessAdminUserId { get; set; }
}