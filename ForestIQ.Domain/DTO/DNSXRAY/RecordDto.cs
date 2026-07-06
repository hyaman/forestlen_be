using System;

namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class RecordDto
    {
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public string? ZoneType { get; set; }
        public bool IsDsIntegrated { get; set; }
        public string? ReplicationScope { get; set; }
        public string? RecordName { get; set; }
        public string? FQDN { get; set; }
        public string? RecordType { get; set; }
        public string? RecordData { get; set; }
        public DateTime? Timestamp { get; set; }
        public int? AgeDays { get; set; }
        public string? AgeStatus { get; set; }
        public double TTL { get; set; }
    }
}
