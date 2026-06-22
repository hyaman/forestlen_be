using System.ComponentModel.DataAnnotations;

namespace ForestIQ.Domain.DTO
{
    public class ConfigureRequest
    {
        [Required]
        public string ForestName { get; init; } = string.Empty;

        [Required]
        public string UserName { get; init; } = string.Empty;

        [Required]
        public string Password { get; init; } = string.Empty;

        public string? DnsServer { get; init; }

        public string? RemoteHost { get; init; }
    }
}
