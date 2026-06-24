using System.Net;
using System.Text.Json;

namespace ForestIQ.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next,ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, ex.Message);

                await WriteResponseAsync(
                    context,
                    HttpStatusCode.Unauthorized,
                    ex.Message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, ex.Message);

                await WriteResponseAsync(
                    context,
                    HttpStatusCode.BadRequest,
                    ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, ex.Message);

                await WriteResponseAsync(
                    context,
                    HttpStatusCode.NotFound,
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                await WriteResponseAsync(
                    context,
                    HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.",
                    ex.Message);
            }
        }

        private static async Task WriteResponseAsync(HttpContext context,HttpStatusCode statusCode,string message,string ErrorMessage = "")
        {
            context.Response.ContentType =
                "application/json";

            context.Response.StatusCode =
                (int)statusCode;

            var response = new
            {
                Data = "",
                Success = false,
                Message = message,
                ErrorMesasge = ErrorMessage
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
