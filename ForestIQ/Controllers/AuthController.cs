using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using ForestIQ.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ForestIQ.Controllers
{
    public class AuthController : ControllerBase
    {
        private readonly IAdConnectionCache _cache;
        private readonly IJwtService _jwtService;
        private readonly IPowerShellService _powerShellService;
        private readonly IConfigureService _configureService;
        private readonly IUserService _userService;
        private readonly IEncryptionService _encryptionService;
        private readonly IConfiguration _configuration;

        public AuthController(IAdConnectionCache cache, IJwtService jwtService, IPowerShellService powerShellService, IConfigureService configureService, IUserService userService, IEncryptionService encryptionService, IConfiguration configuration)
        {
            _cache = cache;
            _jwtService = jwtService;
            _powerShellService = powerShellService;
            _configureService = configureService;
            _userService = userService;
            _encryptionService = encryptionService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            var isSuperAdmin = false;
            string? adPassword = null;

            // 1. Check Super Admin
            var superAdmin = await _userService.GetUserByEmailAsync(loginRequest.UserName);

            if (superAdmin != null && _encryptionService.Unprotect(superAdmin.EncryptedPassword) == loginRequest.Password)
            {
                isSuperAdmin = true;
                adPassword = loginRequest.Password; // Assume the super admin's password works for AD if needed, or we rely on saved AdConfiguration
            }

            // Get Configuration
            var configuration = await _configureService.GetConfigurationAsync();

            if (configuration == null || !configuration.Success || configuration.AdConfiguration == null)
            {
                return Ok(new
                {
                    Success = true,
                    Token = "",
                    RequireConfiguration = true,
                    Message = isSuperAdmin ? "Super Admin logged in. Active Directory configuration is required." : "Configuration Not Found. Please Log in with correct credentials",
                    Error = null as string,
                    ConnectedHost = null as string
                });
            }


            // 2. Check AdConfiguration Credentials
            var savedPassword = configuration.AdConfiguration?.EncryptedPassword; // ConfigureService already calls Unprotect()
            if (configuration.AdConfiguration.UserName.Equals(loginRequest.UserName, StringComparison.OrdinalIgnoreCase) && savedPassword == loginRequest.Password)
            {
                adPassword = loginRequest.Password;
            }
            else if (!isSuperAdmin)
            {
                // Disallow general AD authentication as per user request
                return Unauthorized(new
                {
                    Success = false,
                    Token = "",
                    RequireConfiguration = false,
                    Message = "Invalid credentials. Only Super Admin or the configured Service Account may log in.",
                    Error = null as string,
                    ConnectedHost = null as string
                });
            }

            var savedAdPassword = configuration.AdConfiguration!.EncryptedPassword;

            var request = new PowerShellRequest
            {
                DomainName = configuration.AdConfiguration.ForestName ?? "",
                UserName = configuration.AdConfiguration.UserName, // Always use the service account
                Password = savedAdPassword, // Always use the service account password
                RemoteHost = configuration.AdConfiguration.RemoteHost ?? "",
                DnsServers = JsonSerializer.Deserialize<List<string>>(configuration.AdConfiguration.DnsServersJson ?? "[]") ?? new List<string>()
            };

            PowerShellExecutionResult? res = null;
            var isDebug = _configuration.GetValue<bool>("Debug:Enabled", true);

            res = await _powerShellService.ConnectAsync(request);

            if (res == null || !res.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    Token = "",
                    RequireConfiguration = false,
                    Message = res?.Message ?? "Failed to connect to any domain controller. Please check the configured AD credentials and try again.",
                    Error = res?.Error,
                    ConnectedHost = null as string
                });
            }

            var connectionId = await _cache.Create(request, res.KerberosCachePath);

            var token = _jwtService.GenerateToken(connectionId);

            return Ok(new
            {
                Success = true,
                Token = token,
                RequireConfiguration = false,
                Message = string.IsNullOrWhiteSpace(res.ConnectedHost)
                    ? "Successfully connected to the remote host."
                    : $"Successfully connected to {res.ConnectedHost}.",
                Error = null as string,
                ConnectedHost = res.ConnectedHost
            });
        }

        [HttpPost("discover-hosts")]
        public async Task<IActionResult> DiscoverHosts([FromBody] DomainDiscoveryRequest loginRequest)
        {
            var request = new PowerShellRequest
            {
                DomainName = loginRequest.ForestName ?? "",
                UserName = loginRequest.UserName,
                Password = loginRequest.Password,
                DnsServers = new List<string>(loginRequest.DnsServers?.Split(',') ?? new string[0])
            };

            if (string.IsNullOrWhiteSpace(request.DomainName))
            {
                return BadRequest(new { Success = false, Message = "Domain name is required." });
            }

            var discovery = await _powerShellService.DiscoverHostsAsync(request);

            if (discovery.Hosts != null && discovery.Hosts.Count > 0 && discovery.DomainControllers != null && discovery.DomainControllers.Count > 0)
            {
                return Ok(new
                {
                    Success = true,
                    StatusCode = 200,
                    Data = discovery.DomainControllers,
                    Error = discovery.DomainControllerDiscoveryError
                });
            }

            return NotFound(new
            {
                Success = false,
                StatusCode = 404,
                Data = discovery.DomainControllers,
                Error = discovery.DomainControllerDiscoveryError
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            var connectionId = User.FindFirst("connectionId")?.Value;

            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                _cache.Remove(connectionId);
            }

            _powerShellService.ClearCache();

            return Ok(new
            {
                Success = true
            });
        }
    }
}
