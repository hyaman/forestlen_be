using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace ForestIQ.Service
{
    public class AdConnectionCache : IAdConnectionCache
    {
        private readonly IMemoryCache _cache;
        private readonly IEncryptionService _encryptionService;
        private readonly IKerberosService _kerberosService;

        public AdConnectionCache(IMemoryCache cache,IEncryptionService encryptionService,IKerberosService kerberosService)
        {
            _cache = cache;
            _encryptionService = encryptionService;
            _kerberosService = kerberosService;
        }

        public async Task<string> Create(PowerShellRequest request, string? kerberosCachePath = null)
        {
            var connectionId = Guid.NewGuid().ToString("N");

            var connection = new AdConnection
            {
                ConnectionId = connectionId,
                DomainName = request.DomainName,
                UserName = request.UserName,
                RemoteHost = request.RemoteHost ?? "",
                EncryptedPassword = _encryptionService.Protect(request.Password),
                KerberosCachePath = kerberosCachePath,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(1440)
            };

            _cache.Set(connectionId,connection,TimeSpan.FromMinutes(1440));

            return connectionId;
        }

        public AdConnection? Get(string connectionId)
        {
            return _cache.Get<AdConnection>(connectionId);
        }

        public void Remove(string connectionId)
        {
            if (_cache.TryGetValue(connectionId, out AdConnection? connection))
            {
                _kerberosService.DestroyTicket(connection?.KerberosCachePath);
            }

            _cache.Remove(connectionId);
        }
    }
}
