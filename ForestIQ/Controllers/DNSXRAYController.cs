using ForestIQ.Domain.DTO;
using ForestIQ.Domain.DTO.DNSXRAY;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DNSXRAYController : ControllerBase
    {
        private readonly IDNSXRAYService _service;

        public DNSXRAYController(IDNSXRAYService service)
        {
            _service = service;
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
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetRecordsAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No records found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("record-statistics")]
        public async Task<IActionResult> GetRecordStatistics([FromBody] DnsXrayFilterRequest filter)
        {
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetRecordStatisticsAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No record statistics found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("duplicate-records")]
        public async Task<IActionResult> GetDuplicateRecords([FromBody] DnsXrayFilterRequest filter)
        {
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetDuplicateRecordsAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No duplicate records found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("record-integrity")]
        public async Task<IActionResult> GetRecordIntegrity([FromBody] DnsXrayFilterRequest filter)
        {
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetRecordIntegrityAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No record integrity findings found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("best-practices")]
        public async Task<IActionResult> GetBestPractices([FromBody] DnsXrayFilterRequest filter)
        {
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetBestPracticesAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No best practice checks found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
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
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetResolutionTestsAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No resolution tests found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
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
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetCleanupCandidatesAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No cleanup candidates found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpPost("cleanup-candidates/count")]
        public async Task<IActionResult> GetCleanupCandidatesCount([FromBody] DnsXrayFilterRequest filter)
        {
            if (string.IsNullOrWhiteSpace(filter.DnsServer))
                return BadRequest(ApiResponse<object>.Fail("DnsServer is required."));

            var result = await _service.GetCleanupCandidatesCountAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No cleanup candidates found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }
        [HttpPost("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary([FromBody] DnsXrayFilterRequest filter)
        {
            var result = await _service.GetDashboardSummaryAsync(filter);
            if (result == null) return StatusCode(404, ApiResponse<object>.Fail("No dashboard summary found.", 404));
            return Ok(ApiResponse<object>.Ok(result));
        }
    }
}
