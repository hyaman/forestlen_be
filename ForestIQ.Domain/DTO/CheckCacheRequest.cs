using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class CheckCacheRequest
    {
        public string section { get; set; }
        public string? domain { get; set; }
        public string? site { get; set; }
        public string? targetDc { get; set; }
        public string? forest { get; set; }
        public string? health { get; set; }
    }
}
