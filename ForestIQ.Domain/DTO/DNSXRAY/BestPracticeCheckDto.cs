namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class BestPracticeCheckDto
    {
        public string? Target { get; set; }
        public string? DnsServer { get; set; }
        public string? ZoneName { get; set; }
        public string? CheckName { get; set; }
        public string? WhatIsChecked { get; set; }
        public string? WhyItMatters { get; set; }
        public string? Result { get; set; }
        public string? Severity { get; set; }
        public string? Recommendation { get; set; }
    }
}
