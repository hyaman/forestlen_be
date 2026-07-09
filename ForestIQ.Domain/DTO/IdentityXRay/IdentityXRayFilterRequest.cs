using System;

namespace ForestIQ.Domain.DTO.IdentityXRay
{
    public class IdentityXRayFilterRequest : DashboardFilterRequest
    {
        public int InactiveDays { get; set; } = 90;
        public int PasswordAgeWarningDays { get; set; } = 180;
        public int PasswordAgeCriticalDays { get; set; } = 365;
        public int ComputerPasswordAgeWarningDays { get; set; } = 45;
        public int ComputerPasswordAgeCriticalDays { get; set; } = 90;
        public int TooManyGroupsThreshold { get; set; } = 20;
        public int LargeGroupThreshold { get; set; } = 300;
        public int MaxSecurityEventsPerDC { get; set; } = 2000;
        public int EventReadTimeoutSeconds { get; set; } = 60;
    }
}
