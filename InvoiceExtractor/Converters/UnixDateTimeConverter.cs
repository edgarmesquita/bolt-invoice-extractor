using System.Text.Json;
using System.Text.Json.Serialization;
using Bolt.Business.InvoiceExtractor.Extensions;

namespace Bolt.Business.InvoiceExtractor.Converters;

public class UnixDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetInt64().FromUnixTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTime());
    }
}