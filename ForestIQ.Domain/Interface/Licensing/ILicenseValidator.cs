using ForestIQ.Domain.Models.Licensing;

namespace ForestIQ.Domain.Interface.Licensing
{
    public interface ILicenseValidator
    {
        bool TryParseLicense(string licenseKeyString, out LicensePayload? payload);
        (bool IsValid, string ErrorMessage) ValidateLicense(LicensePayload payload, string expectedAdForest);
    }
}
