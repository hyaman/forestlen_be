namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class IntegrityFindingDto
    {
        public string? FindingType { get; set; }
        public string? Severity { get; set; }
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public string? RecordName { get; set; }
        public string? RecordType { get; set; }
        public string? RecordData { get; set; }
        public string? Recommendation { get; set; }
    }
}
