// See https://aka.ms/new-console-template for more information

//https://node.bolt.eu/business-portal/businessPortal/getAccessToken?version=BP.11.58&session_id=187880b1667399782
//https://node.bolt.eu/business-portal/businessPortal/startAuthentication?version=BP.11.58&session_id=22985b26-5113-4f1f-a32d-9c11bbf1ef4cb1667402766
//https://node.bolt.eu/business-portal/businessPortal/completeAuthentication?version=BP.11.58&session_id=22985b26-5113-4f1f-a32d-9c11bbf1ef4cb1667402766
//https://node.bolt.eu/business-portal/businessPortal/getAccessToken?version=BP.11.58&session_id=22985b26-5113-4f1f-a32d-9c11bbf1ef4cb1667402766

using Bolt.Business.InvoiceExtractor;

Console.WriteLine("==========================================");
Console.WriteLine("==== Bolt Business Invoice Extractor =====");
Console.WriteLine("==========================================");
Console.WriteLine("");

using var extractor = new Extractor(); 
await extractor.ExtractAsync();