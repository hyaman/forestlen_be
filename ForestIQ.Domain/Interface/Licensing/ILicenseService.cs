using System.Threading.Tasks;
using ForestIQ.Domain.Models.Licensing;

namespace ForestIQ.Domain.Interface.Licensing
{
    public interface ILicenseService
    {
        Task<(bool IsValid, string ErrorMessage)> ValidateCurrentLicenseAsync();
        (bool IsValid, string ErrorMessage, LicensePayload? Payload) ValidateLicenseKey(string licenseKey, string expectedAdForest);
        Task EnsureSetupDateInitializedAsync();
    }
}
