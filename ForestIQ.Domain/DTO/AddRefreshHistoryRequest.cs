using ForestIQ.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class AddRefreshHistoryRequest
    {
        public SectionName SectionName { get; set; }
        public string? JsonData { get; set; }
        public Guid? DiscoverID { get; set; }
        public string? DCName { get; set; }
    }
}
