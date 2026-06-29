using System;
using ForestIQ.Domain.Enums;

namespace ForestIQ.Domain.DTO
{
    public class RefreshHistory
    {
        public int Id { get; set; }
        public SectionName SectionName { get; set; }
        public DateTime RefreshTime { get; set; }
        public string? TriggeredBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
