namespace ForestIQ.Domain.DTO
{
    public class HostDiscoveryResult
    {
        public string Host { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string ConnectionType { get; set; } = "None"; // "HTTP", "HTTPS", or "None"
        public int Port { get; set; }
        public string? HostIp { get; set; }
    }
}
