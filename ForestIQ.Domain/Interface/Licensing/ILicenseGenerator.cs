using ForestIQ.Domain.Models.Licensing;

namespace ForestIQ.Domain.Interface.Licensing
{
    public interface ILicenseGenerator
    {
        string GenerateLicense(LicensePayload payload);
    }
}
