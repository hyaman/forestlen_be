namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class DashboardSummaryDto
    {
        public int TotalDnsServers { get; set; }
        public int ReachableDnsServers { get; set; }
        
        public int TotalZones { get; set; }
        public int AdIntegratedZones { get; set; }
        public int ReverseZones { get; set; }
        
        public int TotalRecords { get; set; }
        
        public int WarningItems { get; set; }
        public int CriticalItems { get; set; }
        
        public int HealthScore { get; set; }
        public string? HealthStatus { get; set; }
    }
}
