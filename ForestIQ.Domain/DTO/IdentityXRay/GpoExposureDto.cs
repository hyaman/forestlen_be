using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class VulnerableGpoDto
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? Indicators { get; set; }
        public DateTime? ModificationTime { get; set; }
    }

    public class GpoExposureDto
    {
        public string? Domain { get; set; }
        public bool ModuleAvailable { get; set; }
        public List<VulnerableGpoDto> VulnerableGPOs { get; set; } = new();
    }
}
