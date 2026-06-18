namespace ForestIQ.Domain.DTO
{
    public class DomainDiscoveryResponse
    {
        public List<HostDiscoveryResult> Hosts { get; set; } = [];

        public List<DomainControllerDiscoveryResult> DomainControllers { get; set; } = [];

        public string? DomainControllerDiscoveryError { get; set; }
    }
}
