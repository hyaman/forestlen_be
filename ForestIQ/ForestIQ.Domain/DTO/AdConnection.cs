namespace ForestIQ.Domain.DTO
{
    public class AdConnection
    {
        public string ConnectionId { get; set; } = string.Empty;

        public string DomainName { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string RemoteHost { get; set; } = string.Empty;

        public string EncryptedPassword { get; set; } = string.Empty;

        public string? KerberosCachePath { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }
    }
}
