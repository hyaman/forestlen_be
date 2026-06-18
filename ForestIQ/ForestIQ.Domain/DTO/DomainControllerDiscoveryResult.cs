namespace ForestIQ.Domain.DTO
{
    public class DomainControllerDiscoveryResult
    {
        public string ForestName { get; set; } = string.Empty;

        public string DomainName { get; set; } = string.Empty;

        public string DCName { get; set; } = string.Empty;

        public string SiteName { get; set; } = string.Empty;

        public string? IPv4Address { get; set; }

        public string? OperatingSystem { get; set; }

        public bool IsGlobalCatalog { get; set; }

        public bool IsReadOnly { get; set; }
    }
}
