namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class ZoneDto
    {
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public string? ZoneType { get; set; }
        public bool IsDsIntegrated { get; set; }
        public string? ReplicationScope { get; set; }
        public bool IsReverseLookupZone { get; set; }
        public string? DynamicUpdate { get; set; }
        public string? SecureSecondaries { get; set; }
        public bool IsPaused { get; set; }
        public bool IsShutdown { get; set; }
    }
}
