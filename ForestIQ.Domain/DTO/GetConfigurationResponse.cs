using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class GetConfigurationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AdConfiguration? AdConfiguration { get; set; }
    }
}
