using System;
using System.Text;
using System.Text.Json;
using ForestIQ.Domain.Interface.Licensing;
using ForestIQ.Domain.Models.Licensing;

namespace ForestIQ.Service.Licensing
{
    public class LicenseGenerator : ILicenseGenerator
    {
        private readonly IRsaHelper _rsaHelper;

        public LicenseGenerator(IRsaHelper rsaHelper)
        {
            _rsaHelper = rsaHelper;
        }

        public string GenerateLicense(LicensePayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var signature = _rsaHelper.SignData(json);
            
            var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return $"FIQLIC-{base64Json}.{signature}";
        }
    }
}
