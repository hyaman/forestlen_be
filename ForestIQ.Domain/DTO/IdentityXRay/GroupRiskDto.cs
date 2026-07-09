using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class GroupRiskDetailDto
    {
        public string? Name { get; set; }
        public string? SamAccountName { get; set; }
        public int? Scope { get; set; }
        public int? Category { get; set; }
        public string? Description { get; set; }
        public string? ManagedBy { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Changed { get; set; }
        public int? DirectMembers { get; set; }
        public int? RecursiveMembers { get; set; }
        public int? AdminCount { get; set; }
        public string? Risks { get; set; }
    }

    public class GroupRiskDto
    {
        public string? Domain { get; set; }
        public List<GroupRiskDetailDto> Groups { get; set; } = new();
        public bool CircularNestingDetected { get; set; }
    }
}
