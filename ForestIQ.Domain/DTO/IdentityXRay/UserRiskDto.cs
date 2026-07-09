using System;
using System.Collections.Generic;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class UserRiskDetailDto
    {
        public string? SamAccountName { get; set; }
        public string? DisplayName { get; set; }
        public string? UPN { get; set; }
        public bool? Enabled { get; set; }
        public DateTime? LastLogonDate { get; set; }
        public string? RealLastLogonDC { get; set; }
        public DateTime? PasswordLastSet { get; set; }
        public int? PasswordAgeDays { get; set; }
        public bool? PasswordNeverExpires { get; set; }
        public bool? LockedOut { get; set; }
        public int? GroupCount { get; set; }
        public bool? IsPrivileged { get; set; }
        public string? PrivilegedGroups { get; set; }
        public bool? InProtectedUsers { get; set; }
        public bool? IsServiceAccount { get; set; }
        public int? SPNCount { get; set; }
        public int? AdminCount { get; set; }
        public string? Risks { get; set; }
    }

    public class UserRiskSummaryDto
    {
        public int Stale { get; set; }
        public int NeverLoggedIn { get; set; }
        public int DisabledWithGroups { get; set; }
        public int PwdNeverExpires { get; set; }
        public int PwdNotRequired { get; set; }
        public int OldPassword { get; set; }
        public int NoPreAuth { get; set; }
        public int DES { get; set; }
        public int SPN { get; set; }
        public int Delegation { get; set; }
        public int TempNoExpiry { get; set; }
    }

    public class UserRiskDto
    {
        public string? Domain { get; set; }
        public List<UserRiskDetailDto> Users { get; set; } = new();
        public UserRiskSummaryDto? RiskSummary { get; set; }
    }
}
