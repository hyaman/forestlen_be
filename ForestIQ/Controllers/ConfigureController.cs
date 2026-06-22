using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using ForestIQ.Service;
using Microsoft.AspNetCore.Mvc;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigureController : ControllerBase
    {
        private readonly IConfigureService _configureService;
        private readonly IAdConnectionCache _cache;
        private readonly IPowerShellService _powerShellService;

        public ConfigureController(IConfigureService configureService, IAdConnectionCache cache, IPowerShellService powerShellService)
        {
            _configureService = configureService;
            _cache = cache;
            _powerShellService = powerShellService;
        }

        [HttpPost]
        [Route("AddUpdateConfiguration")]
        public async Task<IActionResult> Configure([FromBody] ConfigureRequest request)
        {
            var response = await _configureService.ConfigureAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet]
        [Route("GetConfiguration")]
        public async Task<IActionResult> GetConfiguration()
        {
            var response = await _configureService.GetConfigurationAsync();

            if (!response.Success)
            {
                return BadRequest(response);
            }

            //if (response.AdConfiguration != null)
            //{
            //    response.AdConfiguration.EncryptedPassword = "";
            //}

            return Ok(response);
        }

        [HttpDelete]
        [Route("DeleteConfiguration")]
        public async Task<IActionResult> DeleteConfiguration()
        {
            var response = await _configureService.DeleteConfigurationAsync();

            if (!response.Success)
            {
                return BadRequest(response);
            }

            var connectionId = User.FindFirst("connectionId")?.Value;

            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                _cache.Remove(connectionId);
            }

            _powerShellService.ClearCache();

            return Ok(response);
        }
    }
}
