using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CopenhagenCityBikes.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var start = Environment.TickCount64;
            int status = 0;
            try
            {
                await _next(context);
                status = context.Response.StatusCode;
            }
            catch (Exception ex)
            {
                status = 500;
                _logger.LogError(ex, "Unhandled exception in request");
                throw;
            }
            finally
            {
                var elapsed = Environment.TickCount64 - start;
                _logger.LogInformation("Request handled method={method} path={path} status={status} elapsed_ms={elapsed}",
                    context.Request.Method, context.Request.Path, status, elapsed);
            }
        }
    }
}