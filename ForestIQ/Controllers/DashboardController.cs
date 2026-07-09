using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ForestIQ.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ForestIQ.Domain.Models.Dashboard;
using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IRefreshHistoryService _refreshHistoryService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IDashboardService dashboardService, IRefreshHistoryService refreshHistoryService, ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _refreshHistoryService = refreshHistoryService;
            _logger = logger;
        }

        [HttpPost("inventory")]
        public async Task<IActionResult> GetInventory([FromBody] DashboardFilterRequest request)
        {
            var result = await _dashboardService.GetDcInventoryAsync(request);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve inventory data."));
            }
            if (!string.IsNullOrEmpty(result.Error))
            {
                return StatusCode(500, ApiResponse<object>.Fail(result.Error));
            }
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("logon-sessions")]
        public async Task<IActionResult> GetLogonSessions([FromBody] DashboardFilterRequest request)
        {
            var result = await _dashboardService.GetDcLogonSessionsAsync(request);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve logon sessions data."));
            }
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("auth-summary")]
        public async Task<IActionResult> GetAuthSummary([FromBody] DashboardFilterRequest request)
        {
            var result = await _dashboardService.GetDcAuthSummaryAsync(request);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve authentication summary data."));
            }
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("ntds-health")]
        public async Task<IActionResult> GetNtdsHealth([FromBody] DashboardFilterRequest request)
        {
            var result = await _dashboardService.GetDcNtdsHealthAsync(request);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve NTDS health data."));
            }
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("performance")]
        public async Task<IActionResult> GetPerformance([FromBody] DashboardFilterRequest request)
        {

            var result = await _dashboardService.GetDcPerformanceAsync(request);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve performance data."));
            }
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpGet("hierarchy")]
        public async Task<IActionResult> GetHierarchy([FromQuery] string domain = "All", [FromQuery] string site = "All", [FromQuery] bool refreshView = false)
        {
            var hierarchyResults = await _dashboardService.GetDcHierarchyAsync(domain, site, refreshView);
            if (hierarchyResults == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve hierarchy data."));
            }

            var forests = new List<ForestHierarchyModel>();

            foreach (var dc in hierarchyResults)
            {
                if (string.IsNullOrEmpty(dc.FQDN)) continue;

                string forestName = dc.ForestName ?? "UNKNOWN";
                string domainName = dc.DomainName ?? "unknown.local";
                string siteName = dc.SiteName ?? "Default-First-Site-Name";
                string serverName = dc.ServerName ?? dc.FQDN.Split('.')[0];
                
                var forestNode = forests.FirstOrDefault(f => f.Name == forestName);
                if (forestNode == null)
                {
                    forestNode = new ForestHierarchyModel
                    {
                        Id = $"forest-{forestName.ToLower()}",
                        Name = forestName
                    };
                    forests.Add(forestNode);
                }

                var domainNode = forestNode.Domains?.FirstOrDefault(d => d.Name == domainName);
                if (domainNode == null)
                {
                    domainNode = new DomainHierarchyModel
                    {
                        Id = $"domain-{domainName.Replace(".", "-")}",
                        Name = domainName
                    };
                    forestNode.Domains?.Add(domainNode);
                }

                var siteNode = domainNode.Sites?.FirstOrDefault(s => s.Name == siteName);
                if (siteNode == null)
                {
                    siteNode = new SiteHierarchyModel
                    {
                        Id = $"site-{siteName.ToLower()}",
                        Name = siteName
                    };
                    domainNode.Sites?.Add(siteNode);
                }

                siteNode.DomainControllers?.Add(new DcHierarchyModel
                {
                    Id = $"dc-{serverName.ToLower()}",
                    Name = serverName.ToUpper(),
                    Fqdn = dc.FQDN,
                    Ip = dc.IPv4
                });
            }

            return Ok(ApiResponse<List<ForestHierarchyModel>>.Ok(forests));
        }

        [HttpGet("default-dc")]
        public async Task<IActionResult> GetDefaultDc([FromQuery] string domain = "", [FromQuery] bool refreshView = false)
        {
            var result = await _dashboardService.GetDefaultDcAsync(domain, refreshView);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve default DC data."));
            }
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("run-discovery")]
        public async Task<IActionResult> RunDiscovery([FromBody] DashboardFilterRequest filter)
        {
            if (filter.DiscoverId == null || filter.DiscoverId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Fail("DiscoverId is required for running discovery."));
            }

            var overallStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting Deep DC Discovery for DiscoverId: {DiscoverId}", filter.DiscoverId);

            var results = new Dictionary<string, object?>();

            // 1. Get-AD-Hierarchy
            try
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Executing Get-AD-Hierarchy for DiscoverId: {DiscoverId}", filter.DiscoverId);
                var hierarchyResult = await _dashboardService.GetDcHierarchyAsync(filter.Domain, filter.Site, filter.RefreshView);
                sw.Stop();
                results["Get-AD-Hierarchy"] = new { data = hierarchyResult };
                _logger.LogInformation("Completed Get-AD-Hierarchy in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Get-AD-Hierarchy for DiscoverId: {DiscoverId}", filter.DiscoverId);
                results["Get-AD-Hierarchy"] = new { data = new { Error = ex.Message } };
            }

            // 2. Get-AD-Inventory
            try
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Executing Get-AD-Inventory for DiscoverId: {DiscoverId}", filter.DiscoverId);
                var inventoryResult = await _dashboardService.GetDcInventoryAsync(filter);
                sw.Stop();
                results["Get-AD-Inventory"] = new { data = inventoryResult };
                _logger.LogInformation("Completed Get-AD-Inventory in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Get-AD-Inventory for DiscoverId: {DiscoverId}", filter.DiscoverId);
                results["Get-AD-Inventory"] = new { data = new { Error = ex.Message } };
            }

            // 3. Get-AD-Sessions
            try
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Executing Get-AD-Sessions for DiscoverId: {DiscoverId}", filter.DiscoverId);
                var sessionsResult = await _dashboardService.GetDcLogonSessionsAsync(filter);
                sw.Stop();
                results["Get-AD-Sessions"] = new { data = sessionsResult };
                _logger.LogInformation("Completed Get-AD-Sessions in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Get-AD-Sessions for DiscoverId: {DiscoverId}", filter.DiscoverId);
                results["Get-AD-Sessions"] = new { data = new { Error = ex.Message } };
            }

            // 4. Get-AD-Health
            try
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Executing Get-AD-Health for DiscoverId: {DiscoverId}", filter.DiscoverId);
                var healthResult = await _dashboardService.GetDcNtdsHealthAsync(filter);
                sw.Stop();
                results["Get-AD-Health"] = new { data = healthResult };
                _logger.LogInformation("Completed Get-AD-Health in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Get-AD-Health for DiscoverId: {DiscoverId}", filter.DiscoverId);
                results["Get-AD-Health"] = new { data = new { Error = ex.Message } };
            }

            // 5. Get-AD-Performance
            try
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Executing Get-AD-Performance for DiscoverId: {DiscoverId}", filter.DiscoverId);
                var performanceResult = await _dashboardService.GetDcPerformanceAsync(filter);
                sw.Stop();
                results["Get-AD-Performance"] = new { data = performanceResult };
                _logger.LogInformation("Completed Get-AD-Performance in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Get-AD-Performance for DiscoverId: {DiscoverId}", filter.DiscoverId);
                results["Get-AD-Performance"] = new { data = new { Error = ex.Message } };
            }

            // 6. Get-AD-Auth-Summary
            try
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Executing Get-AD-Auth-Summary for DiscoverId: {DiscoverId}", filter.DiscoverId);
                var authSummaryResult = await _dashboardService.GetDcAuthSummaryAsync(filter);
                sw.Stop();
                results["Get-AD-Auth-Summary"] = new { data = authSummaryResult };
                _logger.LogInformation("Completed Get-AD-Auth-Summary in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Get-AD-Auth-Summary for DiscoverId: {DiscoverId}", filter.DiscoverId);
                results["Get-AD-Auth-Summary"] = new { data = new { Error = ex.Message } };
            }

            // Save results to history
            var historyObject = new
            {
                DiscoverId = filter.DiscoverId,
                Timestamp = DateTime.UtcNow.ToString("O"),
                DCName = filter.TargetDc
            };

            string jsonData = JsonSerializer.Serialize(results);

            var addHistoryRequest = new AddRefreshHistoryRequest
            {
                DiscoverID = filter.DiscoverId.Value,
                SectionName = SectionName.DeepDcDiscovery,
                JsonData = jsonData,
                DCName = filter.TargetDc.ToLower()
            };

            await _refreshHistoryService.AddRefreshHistoryAsync(addHistoryRequest);

            overallStopwatch.Stop();
            _logger.LogInformation("Deep DC Discovery completed for DiscoverId: {DiscoverId} in {TotalMilliseconds}ms", filter.DiscoverId, overallStopwatch.ElapsedMilliseconds);

            return Ok(ApiResponse<object>.Ok(new
            {
                DiscoverId = filter.DiscoverId,
                Status = "Completed",
                ExecutionTimeMs = overallStopwatch.ElapsedMilliseconds
            }));
        }
    }
}
