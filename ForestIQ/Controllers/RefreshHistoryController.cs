using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Extensions;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RefreshHistoryController : ControllerBase
    {
        private readonly IRefreshHistoryService _refreshHistoryService;
        private readonly IMemoryCache _memoryCache;
        public RefreshHistoryController(IRefreshHistoryService refreshHistoryService, IMemoryCache memoryCache)
        {
            _refreshHistoryService = refreshHistoryService;
            _memoryCache = memoryCache;
        }

        [HttpPost("add-refresh-history")]
        public async Task<IActionResult> AddRefreshHistory([FromBody] AddRefreshHistoryRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid request."));
            }

            await _refreshHistoryService.AddRefreshHistoryAsync(request);
            return Ok(ApiResponse<string>.Ok("Refresh history added successfully."));
        }

        [AllowAnonymous]
        [HttpGet("get-refresh-history/{section}")]
        public async Task<IActionResult> GetRefreshHistory(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                return Ok(ApiResponse<List<RefreshHistory>>.Ok(new List<RefreshHistory>()));
            }

            var sec = (SectionName)Enum.Parse(typeof(SectionName), section);

            var history = await _refreshHistoryService.GetHistoryAsync(sec);
            return Ok(ApiResponse<List<RefreshHistory>>.Ok(history));
        }

        [HttpGet("get-latest-refresh-history")]
        public async Task<IActionResult> GetLatestRefreshHistory([FromQuery] int HistoryId, [FromQuery] Guid? DiscoveryID, [FromQuery] string? DcName)
        {
            var latest = await _refreshHistoryService.GetLatestAsync(HistoryId, DiscoveryID, DcName);
            return Ok(ApiResponse<RefreshHistory>.Ok(latest));
        }

        [AllowAnonymous]
        [HttpGet("get-available-sections")]
        public IActionResult GetAvailableSections()
        {
            var sections = Enum.GetValues(typeof(SectionName))
                               .Cast<SectionName>()
                               .Select(s => new
                               {
                                   Name = s.ToString(),
                                   Value = s.ToEnumString()
                               })
                               .ToList();

            return Ok(ApiResponse<object>.Ok(sections));
        }

        [AllowAnonymous]
        [HttpPost("check-cache")]
        public async Task<IActionResult> CheckCache(CheckCacheRequest checkCacheRequest)
        {
            if (string.IsNullOrEmpty(checkCacheRequest.section))
            {
                return Ok(ApiResponse<bool>.Ok(false));
            }

            if (!Enum.TryParse(typeof(SectionName), checkCacheRequest.section, true, out var parsedSection) || parsedSection == null)
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid section name."));
            }

            var sec = (SectionName)parsedSection;
            bool cacheExists = false;

            if (sec == SectionName.ForestOverview)
            {
                string d = string.IsNullOrEmpty(checkCacheRequest.domain) ? "all" : checkCacheRequest.domain;
                string s = string.IsNullOrEmpty(checkCacheRequest.site) ? "all" : checkCacheRequest.site;

                cacheExists = _memoryCache.TryGetValue($"AD_DIAGNOSTICS_GRAPH_{d}_{s}", out _);
                if (!cacheExists)
                {
                    cacheExists = _memoryCache.TryGetValue("AD_DIAGNOSTICS_GRAPH_all_all", out _);
                }
            }
            else if (sec == SectionName.DeepDcDiscovery)
            {
                string tDc = string.IsNullOrEmpty(checkCacheRequest.targetDc) ? "All" : checkCacheRequest.targetDc;
                string f = string.IsNullOrEmpty(checkCacheRequest.forest) ? "All" : checkCacheRequest.forest;
                string d = string.IsNullOrEmpty(checkCacheRequest.domain) ? "All" : checkCacheRequest.domain;
                string s = string.IsNullOrEmpty(checkCacheRequest.site) ? "All" : checkCacheRequest.site;
                string h = string.IsNullOrEmpty(checkCacheRequest.health) ? "All" : checkCacheRequest.health;

                string cacheKey = $"Dashboard_Inventory_{tDc}_{f}_{d}_{s}_{h}";
                cacheExists = _memoryCache.TryGetValue(cacheKey, out _);

                if (!cacheExists)
                {
                    cacheExists = _memoryCache.TryGetValue("Dashboard_Inventory_All_All_All_All_All", out _);
                }
            }
            else if (sec == SectionName.DeepDnsHealth)
            {
                string dnsSrv = string.IsNullOrEmpty(checkCacheRequest.dnsServer) ? "All" : checkCacheRequest.dnsServer;
                
                string cacheKey = $"DNSXRAY_Replications_{dnsSrv}";
                
                cacheExists = _memoryCache.TryGetValue(cacheKey, out _);

                if (!cacheExists)
                {
                    cacheExists = _memoryCache.TryGetValue("DNSXRAY_Replications_All_All_All", out _);
                }
            }

            return Ok(ApiResponse<bool>.Ok(cacheExists));
        }
    }
}
