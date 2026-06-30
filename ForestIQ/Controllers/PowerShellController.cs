using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ForestIQ.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PowerShellController : ControllerBase
    {   
        private readonly IPowerShellService _powerShellService;
        private readonly IConfigureService _configureService;

        public PowerShellController(IPowerShellService powerShellService, IConfigureService configureService)
        {
            _powerShellService = powerShellService;
            _configureService = configureService;
        }

        [AllowAnonymous]
        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] LoginRequest loginRequest)
        {

            var configuration = await _configureService.GetConfigurationAsync();

            if(configuration == null || !configuration.Success)
            {
                throw new Exception("Configurations Not Found");
            }

            var request = new PowerShellRequest
            {
                DomainName = configuration.AdConfiguration?.ForestName ?? "",
                UserName = loginRequest.UserName,
                Password = loginRequest.Password,
                RemoteHost = configuration.AdConfiguration?.ForestName ?? ""
            };


            var result = await _powerShellService.ConnectAsync(request);

            return Ok(result);
        }

        [HttpPost("Get-AD-Topology")]
        public async Task<IActionResult> GetAdTopology([FromQuery] bool refreshView = false, [FromQuery] string? domain = null, [FromQuery] string? site = null)
        {
            var graphResponse = await _powerShellService.GetAdTopologyAsync(refreshView, domain, site);

            if (graphResponse == null)
            {
                return StatusCode(502, new { success = false, message = "Failed to retrieve AD topology. The script returned no data." });
            }

            return Ok(graphResponse);
        }

        [HttpPost("Get-AD-Replication")]
        public async Task<IActionResult> GetAdReplication([FromQuery] bool refreshView = false, [FromQuery] string? domain = null, [FromQuery] string? site = null)
        {
            var graphResponse = await _powerShellService.GetAdReplicationAsync(refreshView, domain, site);

            if (graphResponse == null)
            {
                return StatusCode(502, new { success = false, message = "Failed to retrieve AD replication data. The script returned no data." });
            }

            return Ok(graphResponse);
        }

        [HttpPost("Get-AD-Diagnostics")]
        public async Task<IActionResult> GetAdDiagnosticsProgressive([FromQuery] bool refreshView = false, [FromQuery] string? domain = null, [FromQuery] string? site = null)
        {
            var graphResponse = await _powerShellService.GetAdDiagnosticsProgressiveAsync(refreshView, domain, site);

            if (graphResponse == null)
            {
                return StatusCode(502, new { success = false, message = "Failed to retrieve AD diagnostics. The script returned no data." });
            }

            return Ok(graphResponse);
        }

        [HttpGet("Get-AD-Domains")]
        public async Task<IActionResult> GetAdDomains([FromQuery] bool refreshView = false)
        {
            var domains = await _powerShellService.GetAdDomainsAsync(refreshView);
            var result = domains.Select(d => new { label = d, value = d });
            return Ok(new { success = true, data = result });
        }

        [HttpGet("Get-AD-Sites")]
        public async Task<IActionResult> GetAdSites([FromQuery] bool refreshView = false)
        {
            var sites = await _powerShellService.GetAdSitesAsync(refreshView);
            var result = sites.Select(s => new { label = s, value = s });
            return Ok(new { success = true, data = result });
        }

        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] PowerShellExecuteRequest request)
        {
            var result = await _powerShellService.ExecuteCommandAsync(request);

            return Ok(result);
        }

        [HttpPost("execute-script")]
        public async Task<IActionResult> ExecuteScript([FromBody] PowerShellScriptRequest request)
        {
            var result = await _powerShellService.ExecuteScriptAsync(request);

            return Ok(result);
        }

        [HttpGet("commands")]
        public IActionResult GetCommands()
        {
            return Ok(new
            {
                Success = true,
                Commands = _powerShellService.GetAvailableCommands()
            });
        }
    }
}
