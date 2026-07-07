using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class DnsReplicationReportDto
    {
        public List<DnsReplicationPartnerDto>? ReplicationHealth { get; set; }
        public List<DnsReplicationPartnerDto>? DnsPartitionReplication { get; set; }
    }

    public class DnsReplicationPartnerDto
    {
        public string? Source { get; set; }
        public string? Target { get; set; }
        public string? DestinationDNS { get; set; }
        public string? DestinationSite { get; set; }
        public string? SourcePartner { get; set; }
        public string? Partition { get; set; }
        public DateTime? LastAttempt { get; set; }
        public DateTime? LastSuccess { get; set; }
        public double? MinutesSinceSuccess { get; set; }
        public int? ConsecutiveFailures { get; set; }
        public int? LastResult { get; set; }
        public bool? ScheduledSync { get; set; }
        public bool? SyncOnStartup { get; set; }
        public bool? TwoWaySync { get; set; }
        public object? PartnerType { get; set; }
        public string? Status { get; set; }
    }
}
