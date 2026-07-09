using System;

namespace ForestIQ.Domain.DTO
{
    public class JobConfiguration
    {
        public int Id { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int? RetentionDays { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
