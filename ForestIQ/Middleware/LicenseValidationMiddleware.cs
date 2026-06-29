using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ForestIQ.Domain.Interface.Licensing;
using ForestIQ.Domain.DTO;
using System.Text.Json;
using System.Linq;

namespace ForestIQ.Middleware
{
    public class LicenseValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public LicenseValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILicenseService licenseService)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            var whitelistedPaths = new[]
            {
                "/api/Auth/login",
                "/api/Configure",
                "/api/internal/license/generate",
                "/swagger"
            };

            if (whitelistedPaths.Any(p => path.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            await licenseService.EnsureSetupDateInitializedAsync();

            var (isValid, errorMessage) = await licenseService.ValidateCurrentLicenseAsync();
            if (!isValid)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail(errorMessage);
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
                return;
            }

            await _next(context);
        }
    }
}
