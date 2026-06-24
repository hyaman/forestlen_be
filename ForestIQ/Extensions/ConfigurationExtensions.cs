using ForestIQ.Domain;

namespace ForestIQ.Extensions
{
    public static class ConfigurationExtensions
    {
        public static void InitializeRuntime(this IConfiguration configuration)
        {
            var jwt = new JwtSettings();
            configuration.GetSection("Jwt").Bind(jwt);
            Runtime.Jwt = jwt;
            Runtime.AllowedHosts = configuration["AllowedHosts"] ?? string.Empty;

            var ad = new ActiveDirectorySettings();
            configuration.GetSection("ActiveDirectory").Bind(ad);
            Runtime.ActiveDirectory = ad;

            var enc = new EncryptionSettings();
            configuration.GetSection("Encryption").Bind(enc);
            Runtime.Encryption = enc;

            var cache = new CacheSettings();
            configuration.GetSection("CacheSettings").Bind(cache);
            Runtime.Cache = cache;

            var debug = new DebugSettings();
            configuration.GetSection("Debug").Bind(debug);
            Runtime.Debug = debug;
        }
    }
}
