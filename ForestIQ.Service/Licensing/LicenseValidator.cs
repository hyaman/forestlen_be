using System;
using System.Text;
using System.Text.Json;
using ForestIQ.Domain.Interface.Licensing;
using ForestIQ.Domain.Models.Licensing;

namespace ForestIQ.Service.Licensing
{
    public class LicenseValidator : ILicenseValidator
    {
        private readonly IRsaHelper _rsaHelper;

        public LicenseValidator(IRsaHelper rsaHelper)
        {
            _rsaHelper = rsaHelper;
        }

        public bool TryParseLicense(string licenseKeyString, out LicensePayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(licenseKeyString)) return false;

            if (!licenseKeyString.StartsWith("FIQLIC-")) return false;

            var parts = licenseKeyString.Substring(7).Split('.');
            if (parts.Length != 2) return false;

            var base64Json = parts[0];
            var signature = parts[1];

            try
            {
                var jsonBytes = Convert.FromBase64String(base64Json);
                var json = Encoding.UTF8.GetString(jsonBytes);

                if (!_rsaHelper.VerifyData(json, signature))
                {
                    return false;
                }

                payload = JsonSerializer.Deserialize<LicensePayload>(json);
                return payload != null;
            }
            catch
            {
                return false;
            }
        }

        public (bool IsValid, string ErrorMessage) ValidateLicense(LicensePayload payload, string expectedAdForest)
        {
            if (DateTime.UtcNow > payload.ExpiryDate)
            {
                return (false, $"License expired on {payload.ExpiryDate:yyyy-MM-dd}.");
            }

            if (!string.IsNullOrWhiteSpace(payload.ForestName))
            {
                if (string.IsNullOrWhiteSpace(expectedAdForest))
                {
                    return (false, "License requires AD Forest binding but no forest is configured.");
                }

                if (!string.Equals(payload.ForestName, expectedAdForest, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"License is bound to AD Forest '{payload.ForestName}' but current forest is '{expectedAdForest}'.");
                }
            }

            return (true, string.Empty);
        }
    }
}
