using ForestIQ.Domain.DTO;
using ForestIQ.Domain.DTO.DNSXRAY;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ForestIQ.Domain.Enums;
using ForestIQ.Service;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DNSXRAYController : ControllerBase
    {
        private readonly IDNSXRAYService _service;
        private readonly IRefreshHistoryService _refreshHistoryService;
        private readonly ILogger<DNSXRAYController> _logger;
        private readonly IDashboardService _dashboardService;

        public DNSXRAYController(IDNSXRAYService service, IRefreshHistoryService refreshHistoryService, ILogger<DNSXRAYController> logger,IDashboardService dashboardService)
        {
            _service = service;
            _refreshHistoryService = refreshHistoryService;
            _logger = logger;
            _dashboardService = dashboardService;
        }

        [HttpPost("dns-servers")]
        public async Task<IActionResult> GetDnsServers([FromBody] DnsXrayFilterRequest filter)
        {
            var result = await _service.GetDnsServersAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No DNS servers found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("zones")]
        public async Task<IActionResult> GetZones([FromBody] DnsXrayFilterRequest filter)
        {
            var result = await _service.GetZonesAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No zones found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("records")]
        public async Task<IActionResult> GetRecords([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetRecordsAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No records found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        [HttpPost("record-statistics")]
        public async Task<IActionResult> GetRecordStatistics([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetRecordStatisticsAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No record statistics found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        [HttpPost("duplicate-records")]
        public async Task<IActionResult> GetDuplicateRecords([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetDuplicateRecordsAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No duplicate records found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        [HttpPost("record-integrity")]
        public async Task<IActionResult> GetRecordIntegrity([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetRecordIntegrityAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No record integrity findings found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        [HttpPost("best-practices")]
        public async Task<IActionResult> GetBestPractices([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetBestPracticesAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No best practice checks found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        [HttpPost("soa-information")]
        public async Task<IActionResult> GetSoaInformation([FromBody] DnsXrayFilterRequest filter)
        {

            var result = await _service.GetSoaInformationAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No SOA information found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("resolution-tests")]
        public async Task<IActionResult> GetResolutionTests([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetResolutionTestsAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No resolution tests found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        [HttpPost("replications")]
        public async Task<IActionResult> GetReplications([FromBody] DnsXrayFilterRequest filter)
        {
            var result = await _service.GetReplicationsAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No replications found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }
        
        [HttpPost("cleanup-candidates")]
        public async Task<IActionResult> GetCleanupCandidates([FromBody] DnsXrayFilterRequest filter)
        {
            try
            {
                var result = await _service.GetCleanupCandidatesAsync(filter);
                if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No cleanup candidates found.", 404));
                return Ok(ApiResponse<object>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }
        
        [HttpPost("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary([FromBody] DnsXrayFilterRequest filter)
        {
            var result = await _service.GetDashboardSummaryAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No dashboard summary found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("run-discovery")]
        public async Task<IActionResult> RunDiscovery([FromBody] DnsXrayFilterRequest filter)
        {
            if (filter.DiscoverId == null || filter.DiscoverId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Fail("DiscoverId is required for full discovery."));
            }

            var resultData = new Dictionary<string, object?>();

            async Task ExecuteStepAsync<T>(string stepName, Func<Task<T?>> action)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation($"Starting DNSXRAY discovery step: {stepName} for DiscoverId: {filter.DiscoverId}");
                try
                {
                    var result = await action();
                    sw.Stop();
                    _logger.LogInformation($"Completed DNSXRAY discovery step: {stepName} for DiscoverId: {filter.DiscoverId} in {sw.ElapsedMilliseconds}ms");
                    resultData[stepName] = result;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogError(ex, $"Error in DNSXRAY discovery step: {stepName} for DiscoverId: {filter.DiscoverId} after {sw.ElapsedMilliseconds}ms");
                    resultData[stepName] = new { Error = ex.Message };
                }
            }

            var cleanupfilter = new DnsXrayFilterRequest
            {
                DiscoverId = filter.DiscoverId,
                DnsServer = filter.DnsServer,
                ZoneName = filter.ZoneName,
                StaleRecordDays = filter.StaleCleanupRecordDays,
                TargetDc = filter.TargetDc
            };

            await ExecuteStepAsync("Get-AD-Hierarchy", () => _dashboardService.GetDcHierarchyAsync(filter.Domain, filter.Site, filter.RefreshView));
            await ExecuteStepAsync("Get-DNS-Zones", () => _service.GetZonesAsync(filter));
            await ExecuteStepAsync("Get-DNS-Record-Stats", () => _service.GetRecordStatisticsAsync(filter));
            await ExecuteStepAsync("Get-DNS-Records", () => _service.GetRecordsAsync(filter));
            await ExecuteStepAsync("Get-DNS-Servers", () => _service.GetDnsServersAsync(filter));
            await ExecuteStepAsync("Get-DNS-Best-Practices", () => _service.GetBestPracticesAsync(filter));
            await ExecuteStepAsync("Get-DNS-Record-Integrity", () => _service.GetRecordIntegrityAsync(filter));
            await ExecuteStepAsync("Get-DNS-Resolution-Tests", () => _service.GetResolutionTestsAsync(filter));
            await ExecuteStepAsync("Get-DNS-Duplicate-Records", () => _service.GetDuplicateRecordsAsync(filter));
            await ExecuteStepAsync("Get-DNS-Replications", () => _service.GetReplicationsAsync(filter));
            await ExecuteStepAsync("Get-DNS-Dashboard-Summary", () => _service.GetDashboardSummaryAsync(filter));
            await ExecuteStepAsync("Get-DNS-Cleanup-Candidates", () => _service.GetCleanupCandidatesAsync(cleanupfilter));

            var historyObject = new
            {
                DiscoverId = filter.DiscoverId,
                Timestamp = DateTime.UtcNow.ToString("O"),
                DCName = filter.DnsServer?.ToLower() ?? string.Empty,
            };

            string jsonData = JsonSerializer.Serialize(resultData);

            var addHistoryRequest = new AddRefreshHistoryRequest
            {
                SectionName = SectionName.DeepDnsHealth,
                DiscoverID = filter.DiscoverId,
                JsonData = jsonData,
                DCName = filter.DnsServer?.ToLower() ?? string.Empty,
            };

            await _refreshHistoryService.AddRefreshHistoryAsync(addHistoryRequest);

            return Ok(ApiResponse<object>.Ok(historyObject));
        }
    }
}
