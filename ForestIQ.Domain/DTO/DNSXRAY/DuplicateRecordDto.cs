namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class DuplicateRecordDto
    {
        public string? DuplicateType { get; set; }
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public string? FQDN { get; set; }
        public string? RecordType { get; set; }
        public string? RecordData { get; set; }
        public int Count { get; set; }
        public string? Recommendation { get; set; }
    }
}
