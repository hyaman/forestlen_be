using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class PowerShellResponse
    {
        public bool Success { get; set; }

        public JsonElement? Data { get; set; }

        public string? Error { get; set; }
    }
}
