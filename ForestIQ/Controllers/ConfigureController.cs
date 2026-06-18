using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigureController : ControllerBase
    {
        private readonly IConfigureService _configureService;

        public ConfigureController(IConfigureService configureService)
        {
            _configureService = configureService;
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

            if (response.AdConfiguration != null)
            {
                response.AdConfiguration.EncryptedPassword = "";
            }

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
            return Ok(response);
        }
    }
}
