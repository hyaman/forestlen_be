using System.ComponentModel.DataAnnotations;

namespace ForestIQ.Domain.DTO
{
    public class PowerShellRequest
    {
        [Required]
        public string DomainName { get; init; } = string.Empty;

        [Required]
        public string UserName { get; init; } = string.Empty;

        [Required]
        public string Password { get; init; } = string.Empty;

        public string? RemoteHost { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string? LocalNetworkSubnet { get; set; }

        public string? KerberosCachePath { get; set; }

        public string? Dns { get; init; }

        public string? DnsServer { get; init; }

        public List<string> DnsServers { get; init; } = [];

        public string? FilterDomain { get; set; }
        
        public string? FilterSite { get; set; }
    }
}
