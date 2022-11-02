namespace Bolt.Business.InvoiceExtractor.Models;

public abstract class BoltResponse<TResponse>
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public TResponse? Data { get; set; }
}