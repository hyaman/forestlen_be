using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.PowerShell
{
    // Intended to be reused when script results are stored in the database in the future.
    public class ScriptErrorData
    {
        [JsonPropertyName("Section")]
        public string? Section { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }
    }

    public class ScriptTimingData
    {
        [JsonPropertyName("Section")]
        public string? Section { get; set; }

        [JsonPropertyName("ElapsedSeconds")]
        public double? ElapsedSeconds { get; set; }
    }

    [JsonConverter(typeof(PowerShellEnumConverter))]
    public class PowerShellEnumData
    {
        public int? IntValue { get; set; }

        public string? Value { get; set; }
    }

    public class PowerShellEnumConverter : JsonConverter<PowerShellEnumData>
    {
        public override PowerShellEnumData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var data = new PowerShellEnumData();

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.TryGetProperty("value", out var valElement) && valElement.ValueKind == JsonValueKind.Number)
                {
                    data.IntValue = valElement.GetInt32();
                }
                if (document.RootElement.TryGetProperty("Value", out var stringValElement) && stringValElement.ValueKind == JsonValueKind.String)
                {
                    data.Value = stringValElement.GetString();
                }
            }
            return data;
        }

        public override void Write(Utf8JsonWriter writer, PowerShellEnumData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value.IntValue.HasValue)
            {
                writer.WriteNumber("value", value.IntValue.Value);
            }
            if (value.Value != null)
            {
                writer.WriteString("Value", value.Value);
            }
            writer.WriteEndObject();
        }
    }
}
