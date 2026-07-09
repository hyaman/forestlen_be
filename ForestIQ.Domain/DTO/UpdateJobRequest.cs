using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class UpdateJobRequest
    {
        public string CronExpression { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int? RetentionDays { get; set; }
        public string jobName { get; set; } = string.Empty;
    }
}
