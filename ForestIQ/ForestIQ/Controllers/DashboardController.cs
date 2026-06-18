using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ForestIQ.Service;
using Microsoft.AspNetCore.Authorization;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory([FromQuery] string targetDc = "All")
        {
            var result = await _dashboardService.GetDcInventoryAsync(targetDc);
            if (result == null)
            {
                return StatusCode(500, "Failed to retrieve inventory data.");
            }
            return Ok(result);
        }

        [HttpGet("logon-sessions")]
        public async Task<IActionResult> GetLogonSessions([FromQuery] string targetDc = "All")
        {
            var result = await _dashboardService.GetDcLogonSessionsAsync(targetDc);
            if (result == null)
            {
                return StatusCode(500, "Failed to retrieve logon sessions data.");
            }
            return Ok(result);
        }

        [HttpGet("auth-summary")]
        public async Task<IActionResult> GetAuthSummary([FromQuery] string targetDc = "All", [FromQuery] int lookBackHours = 24)
        {
            var result = await _dashboardService.GetDcAuthSummaryAsync(targetDc, lookBackHours);
            if (result == null)
            {
                return StatusCode(500, "Failed to retrieve authentication summary data.");
            }
            return Ok(result);
        }

        [HttpGet("ntds-health")]
        public async Task<IActionResult> GetNtdsHealth([FromQuery] string targetDc = "All")
        {
            var result = await _dashboardService.GetDcNtdsHealthAsync(targetDc);
            if (result == null)
            {
                return StatusCode(500, "Failed to retrieve NTDS health data.");
            }
            return Ok(result);
        }
    }
}
