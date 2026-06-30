using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Enums;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ForestIQ.Domain.Extensions;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RefreshHistoryController : ControllerBase
    {
        private readonly IRefreshHistoryService _refreshHistoryService;

        public RefreshHistoryController(IRefreshHistoryService refreshHistoryService)
        {
            _refreshHistoryService = refreshHistoryService;
        }

        public class AddRefreshHistoryRequest
        {
            public SectionName SectionName { get; set; }
        }

        [HttpPost("add-refresh-history")]
        public async Task<IActionResult> AddRefreshHistory([FromBody] AddRefreshHistoryRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid request."));
            }

            await _refreshHistoryService.AddRefreshHistoryAsync(request.SectionName, null);
            return Ok(ApiResponse<string>.Ok("Refresh history added successfully."));
        }

        [AllowAnonymous]
        [HttpGet("get-refresh-history/{section}")]
        public async Task<IActionResult> GetRefreshHistory(string section)
        {
            if(string.IsNullOrEmpty(section))
            {
                return Ok(ApiResponse<List<RefreshHistory>>.Ok(new List<RefreshHistory>()));
            }

            var sec = (SectionName)Enum.Parse(typeof(SectionName), section);

            var history = await _refreshHistoryService.GetHistoryAsync(sec);
            return Ok(ApiResponse<List<RefreshHistory>>.Ok(history));
        }

        [HttpGet("get-latest-refresh-history/{section}")]
        public async Task<IActionResult> GetLatestRefreshHistory(SectionName section)
        {
            if (!Enum.IsDefined(typeof(SectionName), section))
            {
                return BadRequest(ApiResponse<object>.Fail("Invalid section."));
            }

            var latest = await _refreshHistoryService.GetLatestAsync(section);
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
    }
}
