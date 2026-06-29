using ForestIQ.Domain.Interface.Licensing;
using ForestIQ.Domain.Models.Licensing;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace ForestIQ.Controllers.Internal
{
    [ApiController]
    [Route("api/internal/license")]
    public class LicenseController : ControllerBase
    {
        private readonly ILicenseGenerator _generator;

        public LicenseController(ILicenseGenerator generator)
        {
            _generator = generator;
        }

        [HttpPost("generate")]
        public IActionResult Generate([FromBody] LicensePayload payload)
        {
            var apiKey = Request.Headers["X-API-KEY"].FirstOrDefault();
            if (apiKey != "forestiq-internal-gen-key-2026")
            {
                return Unauthorized("Invalid API Key");
            }

            try
            {
                var licenseKey = _generator.GenerateLicense(payload);

                return Ok(new
                {
                    license_key = licenseKey,
                    customer_name = payload.CustomerName,
                    forest_name = payload.ForestName,
                    expiry_date = payload.ExpiryDate.ToString("yyyy-MM-dd")
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Failed to generate license: {ex.Message}");
            }
        }
    }
}
