using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Domain.DTO
{
    public class DnsSnapshot
    {
        public List<string> RuntimeDnsServers { get; set; } = [];

        public string? ResolvConf { get; set; }
    }
}
