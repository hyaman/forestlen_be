using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class ComputerRiskDetailDto
    {
        public string? Name { get; set; }
        public bool? Enabled { get; set; }
        public string? OS { get; set; }
        public DateTime? LastLogonDate { get; set; }
        public DateTime? PasswordLastSet { get; set; }
        public int? PasswordAgeDays { get; set; }
        public int? SPNCount { get; set; }
        public int? AdminCount { get; set; }
        public object? LegacyLapsExpiration { get; set; }
        public object? WindowsLapsExpiration { get; set; }
        public string? Risks { get; set; }
    }

    public class ComputerRiskDto
    {
        public string? Domain { get; set; }
        public List<ComputerRiskDetailDto> Computers { get; set; } = new();
        public bool LapsSchemaDetected { get; set; }
    }
}
