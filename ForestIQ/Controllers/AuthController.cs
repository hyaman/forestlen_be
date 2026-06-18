using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ForestIQ.Controllers
{
    public class AuthController : ControllerBase
    {
        private readonly IAdConnectionCache _cache;
        private readonly IJwtService _jwtService;
        private readonly IPowerShellService _powerShellService;
        private readonly IConfigureService _configureService;
        public AuthController(IAdConnectionCache cache,IJwtService jwtService, IPowerShellService powerShellService, IConfigureService configureService)
        {
            _cache = cache;
            _jwtService = jwtService;
            _powerShellService = powerShellService;
            _configureService = configureService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody]LoginRequest loginRequest)
        {

            var configuration = await _configureService.GetConfigurationAsync();

            if (configuration == null || !configuration.Success)
            {
                throw new UnauthorizedAccessException("Configurations Not Found");
            }

            var request = new PowerShellRequest
            {
                DomainName = configuration.AdConfiguration?.ForestName ?? "",
                UserName = loginRequest.UserName,
                Password = loginRequest.Password,
                RemoteHost = loginRequest.RemoteHost ?? "",
                DnsServers = JsonSerializer.Deserialize<List<string>>(configuration.AdConfiguration?.DnsServersJson ?? "") ?? new List<string>()
            };

            var res = await _powerShellService.ConnectAsync(request);

            if (res == null || !res.Success) {

                return BadRequest(new
                {
                    Success = false,
                    Token = "",
                    Message = res?.Message ?? "Failed to connect to any domain controller. Please check your credentials and try again.",
                    Error = res?.Error
                });
            }

            var connectionId =  await _cache.Create(request, res.KerberosCachePath);

            var token =_jwtService.GenerateToken(connectionId);

            return Ok(new
            {
                Success = true,
                Token = token,
                ConnectedHost = res.ConnectedHost,
                Message = string.IsNullOrWhiteSpace(res.ConnectedHost)
                    ? "Successfully connected to the remote host."
                    : $"Successfully connected to {res.ConnectedHost}."
            });
        }

        [HttpPost("discover-hosts")]
        public async Task<IActionResult> DiscoverHosts([FromBody] LoginRequest loginRequest)
        {

            var configuration = await _configureService.GetConfigurationAsync();

            if (configuration == null || !configuration.Success)
            {
                throw new Exception("Configurations Not Found");
            }

            var request = new PowerShellRequest
            {
                DomainName = configuration.AdConfiguration?.ForestName ?? "",
                UserName = loginRequest.UserName,
                Password = loginRequest.Password,
                RemoteHost = loginRequest.RemoteHost ?? "",
                DnsServers = JsonSerializer.Deserialize<List<string>>(configuration.AdConfiguration?.DnsServersJson ?? "") ?? new List<string>()
            };

            if (string.IsNullOrWhiteSpace(request.DomainName))
            {
                return BadRequest(new { Success = false, Message = "Domain name is required." });
            }

            var discovery = await _powerShellService.DiscoverHostsAsync(request);
            
            return Ok(new
            {
                Success = true,
                StatusCode = 200,
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
