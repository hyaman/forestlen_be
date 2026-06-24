using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using ForestIQ.Service;
using Microsoft.AspNetCore.Authorization;
using ForestIQ.Domain.Models.Dashboard;
using ForestIQ.Domain.DTO;

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

        [HttpPost("inventory")]
        public async Task<IActionResult> GetInventory([FromBody] DashboardFilterRequest request)
        {
            var result = await _dashboardService.GetDcInventoryAsync(request);
            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve inventory data."));
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
    }
}
