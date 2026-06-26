using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    public class DefaultDcModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("FQDN")]
        public string? FQDN { get; set; }

        [JsonPropertyName("IPv4")]
        public string? IPv4 { get; set; }

        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("DomainName")]
        public string? DomainName { get; set; }

        [JsonPropertyName("ForestName")]
        public string? ForestName { get; set; }

        [JsonPropertyName("IsGlobalCatalog")]
        public bool? IsGlobalCatalog { get; set; }

        [JsonPropertyName("IsReadOnly")]
        public bool? IsReadOnly { get; set; }

        [JsonPropertyName("OperatingSystem")]
        public string? OperatingSystem { get; set; }
    }
}
