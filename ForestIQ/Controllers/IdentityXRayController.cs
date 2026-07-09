using ForestIQ.Domain.DTO;
using ForestIQ.Domain.DTO.IdentityXRay;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IdentityXRayController : ControllerBase
    {
        private readonly IIdentityXRayService _identityXRayService;
        private readonly IRefreshHistoryService _refreshHistoryService;

        public IdentityXRayController(IIdentityXRayService identityXRayService, IRefreshHistoryService refreshHistoryService)
        {
            _identityXRayService = identityXRayService;
            _refreshHistoryService = refreshHistoryService;
        }

        private async Task SaveHistoryAsync(Guid? discoverId, object result)
        {
            if (discoverId.HasValue && result != null)
            {
                var historyRequest = new AddRefreshHistoryRequest
                {
                    DiscoverID = discoverId,
                    SectionName = SectionName.IdentityXRay,
                    JsonData = JsonSerializer.Serialize(result)
                };
                await _refreshHistoryService.AddRefreshHistoryAsync(historyRequest);
            }
        }

        [HttpPost("domain-posture")]
        public async Task<IActionResult> GetDomainPosture([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetDomainPostureAsync(request);
            if (result == null) return NotFound("Domain posture data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("user-risk")]
        public async Task<IActionResult> GetUserRisk([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetUserRiskAsync(request);
            if (result == null) return NotFound("User risk data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("group-risk")]
        public async Task<IActionResult> GetGroupRisk([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetGroupRiskAsync(request);
            if (result == null) return NotFound("Group risk data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("computer-risk")]
        public async Task<IActionResult> GetComputerRisk([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetComputerRiskAsync(request);
            if (result == null) return NotFound("Computer risk data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("acl-exposure")]
        public async Task<IActionResult> GetAclExposure([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetAclExposureAsync(request);
            if (result == null) return NotFound("ACL exposure data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("gpo-exposure")]
        public async Task<IActionResult> GetGpoExposure([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetGpoExposureAsync(request);
            if (result == null) return NotFound("GPO exposure data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("logon-intelligence")]
        public async Task<IActionResult> GetLogonIntelligence([FromBody] IdentityXRayFilterRequest request)
        {
            var result = await _identityXRayService.GetLogonIntelligenceAsync(request);
            if (result == null) return NotFound("Logon intelligence data could not be retrieved.");

            await SaveHistoryAsync(request.DiscoverId, result);
            return Ok(new { data = result });
        }

        [HttpPost("run-discovery")]
        public async Task<IActionResult> RunDiscovery([FromBody] IdentityXRayFilterRequest request)
        {
            var results = new Dictionary<string, object>();
            var errors = new List<string>();

            async Task ExecuteServiceCall<T>(string name, Func<Task<T?>> serviceCall) where T : class
            {
                try
                {
                    var result = await serviceCall();
                    if (result != null)
                    {
                        results[name] = new { data = result };
                    }
                    else
                    {
                        errors.Add($"{name} failed or returned null.");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{name} threw an exception: {ex.Message}");
                }
            }

            await ExecuteServiceCall("Get-IdentityXRay-DomainPosture", () => _identityXRayService.GetDomainPostureAsync(request));
            await ExecuteServiceCall("Get-IdentityXRay-UserRisk", () => _identityXRayService.GetUserRiskAsync(request));
            await ExecuteServiceCall("Get-IdentityXRay-GroupRisk", () => _identityXRayService.GetGroupRiskAsync(request));
            await ExecuteServiceCall("Get-IdentityXRay-ComputerRisk", () => _identityXRayService.GetComputerRiskAsync(request));
            await ExecuteServiceCall("Get-IdentityXRay-AclExposure", () => _identityXRayService.GetAclExposureAsync(request));
            await ExecuteServiceCall("Get-IdentityXRay-GpoExposure", () => _identityXRayService.GetGpoExposureAsync(request));
            await ExecuteServiceCall("Get-IdentityXRay-LogonIntelligence", () => _identityXRayService.GetLogonIntelligenceAsync(request));

            await SaveHistoryAsync(request.DiscoverId, results);

            return Ok(new { Results = results, Errors = errors });
        }
    }
}
