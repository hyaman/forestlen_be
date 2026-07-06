namespace ForestIQ.Domain.DTO.DNSXRAY
{
    public class ResolutionTestDto
    {
        public string? DnsServer { get; set; }
        public string? QueryName { get; set; }
        public string? Status { get; set; }
        public int AnswerCount { get; set; }
        public string? ResultSummary { get; set; }
        public string? Recommendation { get; set; }
    }
}
