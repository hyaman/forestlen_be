namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class SoaComparisonDto
    {
        public string? ZoneName { get; set; }
        public string? DnsServer { get; set; }
        public string? PrimaryServer { get; set; }
        public int SerialNumber { get; set; }
        public string? Status { get; set; }
    }
}
