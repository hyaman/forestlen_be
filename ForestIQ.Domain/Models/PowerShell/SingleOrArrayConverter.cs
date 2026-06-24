using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.PowerShell
{
    public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new List<T>();
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<T>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    var item = JsonSerializer.Deserialize<T>(ref reader, options);
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
                return list;
            }

            var singleItem = JsonSerializer.Deserialize<T>(ref reader, options);
            if (singleItem != null)
            {
                return new List<T> { singleItem };
            }

            return new List<T>();
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Count == 1)
            {
                JsonSerializer.Serialize(writer, value[0], options);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var item in value)
                {
                    JsonSerializer.Serialize(writer, item, options);
                }
                writer.WriteEndArray();
            }
        }
    }
}
