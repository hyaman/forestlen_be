using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SectionName
    {
        [EnumMember(Value = "forest-overview")]
        ForestOverview,

        [EnumMember(Value = "deep-dc-discovery")]
        DeepDcDiscovery
    }
}
