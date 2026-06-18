using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class PowerShellExecutionResult
    {
        public bool Success { get; set; }

        public int StatusCode { get; set; }

        public JsonElement? Data { get; set; }

        public string? Error { get; set; }

        public string? Message { get; set; }

        public string? Output { get; set; }

        public string? KerberosCachePath { get; set; }

        public string? ConnectedHost { get; set; }
    }
}
