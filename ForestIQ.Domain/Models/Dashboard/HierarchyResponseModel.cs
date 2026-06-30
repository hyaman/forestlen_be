using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    public class ForestHierarchyModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("domains")]
        public List<DomainHierarchyModel>? Domains { get; set; } = new List<DomainHierarchyModel>();
    }

    public class DomainHierarchyModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sites")]
        public List<SiteHierarchyModel>? Sites { get; set; } = new List<SiteHierarchyModel>();
    }

    public class SiteHierarchyModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("domainControllers")]
        public List<DcHierarchyModel>? DomainControllers { get; set; } = new List<DcHierarchyModel>();
    }

    public class DcHierarchyModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("fqdn")]
        public string? Fqdn { get; set; }

        [JsonPropertyName("ip")]
        public string? Ip { get; set; }

        [JsonPropertyName("health")]
        public string? Health { get; set; }
    }

    public class DcHierarchyRawModel
    {
        [JsonPropertyName("ForestName")]
        public string? ForestName { get; set; }

        [JsonPropertyName("DomainName")]
        public string? DomainName { get; set; }

        [JsonPropertyName("SiteName")]
        public string? SiteName { get; set; }

        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("FQDN")]
        public string? FQDN { get; set; }

        [JsonPropertyName("IPv4")]
        public string? IPv4 { get; set; }
    }
}
