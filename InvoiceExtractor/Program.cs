// See https://aka.ms/new-console-template for more information

using Bolt.Business.InvoiceExtractor;

Console.WriteLine("==========================================");
Console.WriteLine("==== Bolt Business Invoice Extractor =====");
Console.WriteLine("==========================================");
Console.WriteLine("");

using var extractor = new Extractor(); 
await extractor.ExtractAsync();