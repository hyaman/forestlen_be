using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class DomainControllerDto
    {
        public string? HostName { get; set; }
        public string? Site { get; set; }
        public string? IPv4Address { get; set; }
        public bool? IsGlobalCatalog { get; set; }
        public string? OperatingSystem { get; set; }
    }

    public class DomainTrustDto
    {
        public string? Name { get; set; }
        public int? Direction { get; set; }
        public int? TrustType { get; set; }
        public bool? Transitive { get; set; }
        public bool? SelectiveAuthentication { get; set; }
        public bool? SIDFilteringQuarantined { get; set; }
    }

    public class PasswordPolicyDto
    {
        public bool? ComplexityEnabled { get; set; }
        public int? MinPasswordLength { get; set; }
        public TimeSpan? MaxPasswordAge { get; set; }
        public int? PasswordHistoryCount { get; set; }
        public int? LockoutThreshold { get; set; }
        public TimeSpan? LockoutDuration { get; set; }
        public TimeSpan? LockoutObservationWindow { get; set; }
    }

    public class LapsSchemaStatusDto
    {
        public bool LegacyLapsPassword { get; set; }
        public bool LegacyLapsExpiration { get; set; }
        public bool WindowsLapsPassword { get; set; }
        public bool WindowsLapsEncrypted { get; set; }
        public bool WindowsLapsExpiration { get; set; }
    }

    public class FgppDto
    {
        public string? Name { get; set; }
        public int? Precedence { get; set; }
        public int? MinPasswordLength { get; set; }
        public TimeSpan? MaxPasswordAge { get; set; }
        public int? LockoutThreshold { get; set; }
        public string[]? AppliesTo { get; set; }
    }

    public class ForeignPrincipalDto
    {
        public string? Name { get; set; }
        public string? ObjectClass { get; set; }
    }

    public class DomainPostureDto
    {
        public string? Domain { get; set; }
        public string? DN { get; set; }
        public List<DomainControllerDto> DCs { get; set; } = new();
        public List<DomainTrustDto> Trusts { get; set; } = new();
        public PasswordPolicyDto? PasswordPolicy { get; set; }
        public LapsSchemaStatusDto? LapsSchemaStatus { get; set; }
        public List<FgppDto> FGPPs { get; set; } = new();
        public List<ForeignPrincipalDto> ForeignPrincipals { get; set; } = new();
    }
}
