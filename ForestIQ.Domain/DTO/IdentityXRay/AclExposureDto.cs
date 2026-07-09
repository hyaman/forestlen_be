using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class AclVulnerabilityDto
    {
        public string? Severity { get; set; }
        public string? TargetType { get; set; }
        public string? TargetName { get; set; }
        public string? Identity { get; set; }
        public string? Permission { get; set; }
        public string? DN { get; set; }
        public string? Finding { get; set; }
    }

    public class AclExposureDto
    {
        public string? Domain { get; set; }
        public List<AclVulnerabilityDto> Vulnerabilities { get; set; } = new();
    }
}
