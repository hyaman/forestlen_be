using System;

namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class DnsXrayFilterRequest : DashboardFilterRequest
    {
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public int? StaleRecordDays { get; set; } = 90;
        public int? StaleCleanupRecordDays { get; set; } = 180;
    }
}
