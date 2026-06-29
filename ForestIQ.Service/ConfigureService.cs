using ForestIQ.Domain;
using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ForestIQ.Service
{
    public class ConfigureService : IConfigureService
    {
        private readonly IConfigureRepository _repository;
        private readonly IKerberosService _kerberosService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<ConfigureService> _logger;
        private readonly ForestIQ.Domain.Interface.Licensing.ILicenseValidator _licenseValidator;

        public ConfigureService(IConfigureRepository repository,IKerberosService kerberosService,IEncryptionService encryptionService,ILogger<ConfigureService> logger, ForestIQ.Domain.Interface.Licensing.ILicenseValidator licenseValidator)
        {
            _repository = repository;
            _kerberosService = kerberosService;
            _encryptionService = encryptionService;
            _logger = logger;
            _licenseValidator = licenseValidator;
        }

        public async Task<ConfigureResponse> ConfigureAsync(ConfigureRequest request)
        {
            var dnsServers = CollectDnsServers(request);
            var validationError = ValidateRequest(request, dnsServers);
            if (validationError != null)
            {
                return new ConfigureResponse
                {
                    Success = false,
                    Message = validationError
                };
            }

            var existingConfig = await _repository.GetByForestNameAsync();
            var licensing = existingConfig?.Licensing ?? new ForestIQ.Domain.Models.Licensing.LicensingConfiguration
            {
                ContainerSetupDate = DateTime.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(request.LicenseKey))
            {
                if (!_licenseValidator.TryParseLicense(request.LicenseKey, out var payload) || payload == null)
                {
                    return new ConfigureResponse { Success = false, Message = "Invalid license key format or signature." };
                }

                if (!string.IsNullOrWhiteSpace(payload.ForestName))
                {
                    if (!request.ConfirmEnvironment)
                    {
                        return new ConfigureResponse
                        {
                            Success = false,
                            Message = $"The license is bound to AD Forest '{payload.ForestName}'. Please confirm that your configured Forest Name matches this.",
                            RequiresEnvironmentConfirmation = true,
                            DetectedForest = request.ForestName
                        };
                    }

                    if (!string.Equals(payload.ForestName, request.ForestName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ConfigureResponse { Success = false, Message = $"License is bound to AD Forest '{payload.ForestName}' but you are configuring '{request.ForestName}'." };
                    }
                    
                    licensing.EnvironmentBinding = new ForestIQ.Domain.Models.Licensing.EnvironmentBindingConfig
                    {
                        Confirmed = true,
                        AdForest = request.ForestName
                    };
                }

                licensing.LicenseKey = request.LicenseKey;
            }

            DnsSnapshot? dnsSnapshot = null;
            var cachePath = _kerberosService.BuildCachePath(Guid.NewGuid().ToString("N"));

            try
            {
                dnsSnapshot = await ApplyContainerDnsAsync(dnsServers);

                var ticketResult = await _kerberosService.AcquireTicketAsync(request.ForestName,request.UserName,request.Password,cachePath);

                if (!ticketResult.Success)
                {
                    await RestoreContainerDnsAsync(dnsSnapshot);

                    return new ConfigureResponse
                    {
                        Success = false,
                        Message = $"Connection failed. DNS was reverted. {ticketResult.Error}"
                    };
                }

                
                var configuration = new AdConfiguration
                {
                    Id = existingConfig?.Id ?? 0,
                    ForestName = request.ForestName.Trim(),
                    UserName = request.UserName.Trim(),
                    EncryptedPassword = _encryptionService.Protect(request.Password),
                    DnsServersJson = JsonSerializer.Serialize(dnsServers),
                    RemoteHost = request.RemoteHost?.Trim(),
                    UpdatedAtUtc = DateTime.Now,
                    CreatedAtUtc = existingConfig?.CreatedAtUtc ?? DateTime.Now,
                    Licensing = licensing
                };

                var id = await _repository.UpsertAsync(configuration);

                return new ConfigureResponse
                {
                    Success = true,
                    Message = "Configuration saved successfully.",
                    ConfigurationId = id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configure API failed for forest {ForestName}.", request.ForestName);

                if (dnsSnapshot != null)
                {
                    await RestoreContainerDnsAsync(dnsSnapshot);
                }

                return new ConfigureResponse
                {
                    Success = false,
                    Message = $"Configuration failed. DNS was reverted. {ex.Message}"
                };
            }
            finally
            {
                _kerberosService.DestroyTicket(cachePath);
            }
        }

        public async Task<GetConfigurationResponse> GetConfigurationAsync()
        {
            try
            {
                var configuration = await _repository.GetByForestNameAsync();

                if (configuration == null)
                {
                    return new GetConfigurationResponse
                    {
                        Success = false,
                        Message = $"No configuration found."
                    };
                }

                return new GetConfigurationResponse
                {
                    Success = true,
                    Message = "Configuration retrieved successfully.",
                    AdConfiguration = new AdConfiguration
                    {
                        Id = configuration.Id,
                        ForestName = configuration.ForestName,
                        UserName = configuration.UserName,
                        EncryptedPassword = _encryptionService.Unprotect(configuration.EncryptedPassword),
                        DnsServersJson = configuration.DnsServersJson,
                        RemoteHost = configuration.RemoteHost,
                        CreatedAtUtc = configuration.CreatedAtUtc,
                        UpdatedAtUtc = configuration.UpdatedAtUtc,
                        Licensing = configuration.Licensing
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"Failed to retrieve configuration");

                return new GetConfigurationResponse
                {
                    Success = false,
                    Message = $"Failed to retrieve configuration. {ex.Message}"
                };
            }
        }

        public async Task<ConfigureResponse> DeleteConfigurationAsync()
        {
            try
            {
                var deleted = await _repository.DeleteAsync();

                if (!deleted)
                {
                    return new ConfigureResponse
                    {
                        Success = false,
                        Message = "No configuration found to delete."
                    };
                }

                return new ConfigureResponse
                {
                    Success = true,
                    Message = "Configuration deleted successfully."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete configuration");
                return new ConfigureResponse
                {
                    Success = false,
                    Message = $"Failed to delete configuration. {ex.Message}"
                };
            }
        }

        private string? ValidateRequest(ConfigureRequest request, List<string> dnsServers)
        {
            if (string.IsNullOrWhiteSpace(request.ForestName))
            {
                return "Forest name is required.";
            }

            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                return "Username is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return "Password is required.";
            }

            if (dnsServers.Count == 0)
            {
                return "At least one DNS server is required.";
            }

            foreach (var dnsServer in dnsServers)
            {
                if (!IPAddress.TryParse(dnsServer, out var ipAddress) ||
                    ipAddress.AddressFamily != AddressFamily.InterNetwork ||
                    IPAddress.IsLoopback(ipAddress))
                {
                    return $"Invalid DNS server '{dnsServer}'. Provide a non-loopback IPv4 address.";
                }
            }

            return null;
        }

        private List<string> CollectDnsServers(ConfigureRequest request)
        {
            var values = new List<string>();

            values = request.DnsServer?.Split(',').ToList() ?? new List<string>();


            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value.Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => IPAddress.TryParse(value, out var ipAddress) ? ipAddress.ToString() : value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<DnsSnapshot> ApplyContainerDnsAsync(List<string> dnsServers)
        {
            var snapshot = new DnsSnapshot
            {
                RuntimeDnsServers = Runtime.ActiveDirectory.DnsServers.ToList()
            };

            if (OperatingSystem.IsLinux() && File.Exists("/etc/resolv.conf"))
            {
                snapshot.ResolvConf = await File.ReadAllTextAsync("/etc/resolv.conf");
            }

            Runtime.ActiveDirectory.DnsServers = dnsServers.ToList();

            if (OperatingSystem.IsLinux())
            {
                var resolvConfLines = dnsServers
                    .Select(dnsServer => $"nameserver {dnsServer}")
                    .ToArray();

                await File.WriteAllLinesAsync("/etc/resolv.conf", resolvConfLines);
            }

            return snapshot;
        }

        private async Task RestoreContainerDnsAsync(DnsSnapshot snapshot)
        {
            Runtime.ActiveDirectory.DnsServers = snapshot.RuntimeDnsServers.ToList();

            if (OperatingSystem.IsLinux() && snapshot.ResolvConf != null)
            {
                await File.WriteAllTextAsync("/etc/resolv.conf", snapshot.ResolvConf);
            }
        }
    }
}
