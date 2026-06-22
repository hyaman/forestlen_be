using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public class DomainDiscoveryRequest : LoginRequest
    {
        public string? ForestName { get; set; } = string.Empty;

        public string? DnsServers { get; set; } = string.Empty;
    }
}
