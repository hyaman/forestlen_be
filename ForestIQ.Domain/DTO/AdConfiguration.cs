namespace ForestIQ.Domain.DTO
{
    public class AdConfiguration
    {
        public long Id { get; set; }

        public string ForestName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string EncryptedPassword { get; set; } = string.Empty;

        public string DnsServersJson { get; set; } = "[]";

        public string? RemoteHost { get; set; }

        public DateTime? CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; } = DateTime.Now;
    }
}
