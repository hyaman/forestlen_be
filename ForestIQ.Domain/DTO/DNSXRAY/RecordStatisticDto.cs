namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class RecordStatisticDto
    {
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public string? RecordType { get; set; }
        public int Count { get; set; }
    }
}
