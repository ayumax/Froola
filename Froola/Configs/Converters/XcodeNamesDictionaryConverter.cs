using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Froola.Configs.Collections;

namespace Froola.Configs.Converters;

public class XcodeNamesDictionaryConverter : JsonConverter<OptionDictionary<UEVersion, string>>
{
    public override OptionDictionary<UEVersion, string> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var dict = new OptionDictionary<UEVersion, string>();
        using var doc = JsonDocument.ParseValue(ref reader);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var enumKey = UEVersionExtensions.Parse(prop.Name);
            dict[enumKey] = prop.Value.GetString() ?? string.Empty;
        }

        return dict;
    }

    public override void Write(Utf8JsonWriter writer, OptionDictionary<UEVersion, string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kv in value)
        {
            writer.WriteString(kv.Key.ToVersionString(), kv.Value);
        }

        writer.WriteEndObject();
    }
}