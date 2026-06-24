namespace ForestIQ.Domain
{
    public static class Runtime
    {
        public static JwtSettings Jwt { get; set; } = new();
        public static string AllowedHosts { get; set; } = string.Empty;
        public static ActiveDirectorySettings ActiveDirectory { get; set; } = new();
        public static EncryptionSettings Encryption { get; set; } = new();
    }

    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public class ActiveDirectorySettings
    {
        public List<string> DnsServers { get; set; } = [];
    }

    public class EncryptionSettings
    {
        public string Key { get; set; } = string.Empty;
    }
}
