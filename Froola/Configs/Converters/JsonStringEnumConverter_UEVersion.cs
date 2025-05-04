using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Froola.Configs.Converters;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class JsonStringEnumConverter_UEVersion : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Returns true if the type is UEVersion or Nullable<UEVersion>
        if (typeToConvert == typeof(UEVersion))
        {
            return true;
        }

        return Nullable.GetUnderlyingType(typeToConvert) == typeof(UEVersion);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // Create a converter for UEVersion
        if (typeToConvert == typeof(UEVersion) || Nullable.GetUnderlyingType(typeToConvert) == typeof(UEVersion))
        {
            return new UEVersionStringConverter();
        }

        throw new NotSupportedException($"Cannot convert type {typeToConvert}");
    }

    // Converter for UEVersion <-> string
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private class UEVersionStringConverter : JsonConverter<UEVersion>
    {
        public override UEVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (value == null)
            {
                throw new JsonException("UEVersion string value is null");
            }

            return UEVersionExtensions.Parse(value);
        }

        public override void Write(Utf8JsonWriter writer, UEVersion value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToVersionString());
        }
    }
}