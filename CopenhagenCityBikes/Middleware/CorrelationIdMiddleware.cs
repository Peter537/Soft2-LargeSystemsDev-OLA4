using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace CopenhagenCityBikes.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = Guid.NewGuid().ToString();
            using (LogContext.PushProperty("correlation_id", correlationId))
            {
                await _next(context);
            }
        }
    }
}