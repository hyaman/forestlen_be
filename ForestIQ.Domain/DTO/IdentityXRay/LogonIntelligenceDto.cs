using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class LogonEventDto
    {
        public DateTime? Time { get; set; }
        public int? EventId { get; set; }
        public string? User { get; set; }
        public string? SourceDC { get; set; }
        public string? SourceIp { get; set; }
        public string? Workstation { get; set; }
        public string? AuthType { get; set; }
        public string? Findings { get; set; }
    }

    public class LogonIntelligenceDto
    {
        public string? Domain { get; set; }
        public List<LogonEventDto> LogonEvents { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
