
using Microsoft.AspNetCore.Http;
using NLog;
using System.Net.Http;

namespace CopenhagenCityBikes.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var cid = Guid.NewGuid().ToString();
            var prev = MappedDiagnosticsLogicalContext.Get("correlation_id");
            MappedDiagnosticsLogicalContext.Set("correlation_id", cid);
            try
            {
                await _next(context);
            }
            finally
            {
                if (prev == null)
                    MappedDiagnosticsLogicalContext.Remove("correlation_id");
                else
                    MappedDiagnosticsLogicalContext.Set("correlation_id", prev);
            }
        }
    }
}