using System;
using System.ComponentModel.DataAnnotations;

namespace ForestIQ.Domain.DTO
{
    public class DcPerformanceHistoryEntry
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        public string ServerName { get; set; } = string.Empty;
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public double CpuLoad { get; set; }
        public double MemoryUsage { get; set; }
        
        public double NetworkIo { get; set; } 
    }
}
