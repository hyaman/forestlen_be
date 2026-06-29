using System;
using System.Threading.Tasks;
using ForestIQ.Domain.Interface;
using ForestIQ.Domain.Interface.Licensing;
using ForestIQ.Domain.Models.Licensing;

namespace ForestIQ.Service.Licensing
{
    public class LicenseService : ILicenseService
    {
        private readonly IConfigureRepository _configureRepository;
        private readonly ILicenseValidator _licenseValidator;

        public LicenseService(IConfigureRepository configureRepository, ILicenseValidator licenseValidator)
        {
            _configureRepository = configureRepository;
            _licenseValidator = licenseValidator;
        }

        public async Task<(bool IsValid, string ErrorMessage)> ValidateCurrentLicenseAsync()
        {
            var config = await _configureRepository.GetByForestNameAsync();
            if (config == null)
            {
                // Configuration not complete, so technically licensing isn't active yet,
                // or we are in a state where protected APIs shouldn't be called anyway.
                return (true, string.Empty); 
            }

            var licensing = config.Licensing ?? new LicensingConfiguration();

            if (!string.IsNullOrWhiteSpace(licensing.LicenseKey))
            {
                if (!_licenseValidator.TryParseLicense(licensing.LicenseKey, out var payload) || payload == null)
                {
                    return (false, "Invalid license key format or signature.");
                }

                if (!string.IsNullOrWhiteSpace(payload.ForestName) && (licensing.EnvironmentBinding == null || !licensing.EnvironmentBinding.Confirmed))
                {
                    return (false, "License environment binding has not been confirmed.");
                }

                return _licenseValidator.ValidateLicense(payload, config.ForestName);
            }

            var setupDate = licensing.ContainerSetupDate ?? DateTime.UtcNow;
            
            if ((DateTime.UtcNow - setupDate).TotalDays > 14)
            {
                return (false, $"Temporary evaluation license expired on {setupDate.AddDays(14):yyyy-MM-dd}. Please enter a valid license key.");
            }

            return (true, string.Empty);
        }

        public (bool IsValid, string ErrorMessage, LicensePayload? Payload) ValidateLicenseKey(string licenseKey, string expectedAdForest)
        {
            if (!_licenseValidator.TryParseLicense(licenseKey, out var payload) || payload == null)
            {
                return (false, "Invalid license key format or signature.", null);
            }

            var validation = _licenseValidator.ValidateLicense(payload, expectedAdForest);
            return (validation.IsValid, validation.ErrorMessage, payload);
        }

        public async Task EnsureSetupDateInitializedAsync()
        {
            var config = await _configureRepository.GetByForestNameAsync();
            if (config != null)
            {
                if (config.Licensing == null)
                {
                    config.Licensing = new LicensingConfiguration();
                }

                if (config.Licensing.ContainerSetupDate == null)
                {
                    config.Licensing.ContainerSetupDate = DateTime.UtcNow;
                    await _configureRepository.UpsertAsync(config);
                }
            }
        }
    }
}
