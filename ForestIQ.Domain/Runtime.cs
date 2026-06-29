namespace ForestIQ.Domain
{
    public static class Runtime
    {
        public static JwtSettings Jwt { get; set; } = new();
        public static string AllowedHosts { get; set; } = string.Empty;
        public static ActiveDirectorySettings ActiveDirectory { get; set; } = new();
        public static EncryptionSettings Encryption { get; set; } = new();
        public static CacheSettings Cache { get; set; } = new();
        public static DebugSettings Debug { get; set; } = new();
        public static BackgroundJobSettings BackgroundJobs { get; set; } = new();
        public static LicensingSettings Licensing { get; set; } = new();
    }

    public class LicensingSettings
    {
        public string RsaPrivateKey { get; set; } = string.Empty;
        public string RsaPublicKey { get; set; } = string.Empty;
    }

    public class BackgroundJobSettings
    {
        public string PerformancePollingCron { get; set; } = "0 * * * *";
        public string RefreshHistoryCleanupCron { get; set; } = "0 0 * * *";
        public int RefreshHistoryRetentionDays { get; set; } = 7;
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

    public class CacheSettings
    {
        public int DashboardCacheMinutes { get; set; } = 30;
        public int AdConnectionCacheMinutes { get; set; } = 1440;
    }

    public class DebugSettings
    {
        public bool Enabled { get; set; } = true;
        public bool SaveScriptResponse { get; set; } = true;
    }
}
