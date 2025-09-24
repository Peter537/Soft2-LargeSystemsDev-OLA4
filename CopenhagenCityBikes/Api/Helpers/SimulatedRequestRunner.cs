using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CopenhagenCityBikes.Api.Helpers
{
    public class SimulatedRequestRunner
    {
        private readonly ILogger<SimulatedRequestRunner> _logger;

        public SimulatedRequestRunner(ILogger<SimulatedRequestRunner> logger)
        {
            _logger = logger;
        }

        private async Task<(int status, T? result)> RunInternalAsync<T>(
            Func<Task<T?>> action,
            Func<T?, int> determineStatus,
            string method,
            string path,
            string? correlationId = null,
            string? userId = null)
        {
            var sw = Stopwatch.StartNew();
            T? result = default;
            int status = 500;
            try
            {
                result = await action();
                status = determineStatus(result);
            }
            catch
            {
                status = 500;
            }
            finally
            {
                sw.Stop();
                if (userId is null)
                    _logger.LogInformation("Request handled method={method} path={path} status={status} elapsed_ms={elapsed}",
                        method, path, status, sw.ElapsedMilliseconds);
                else
                    _logger.LogInformation("Request handled method={method} path={path} status={status} elapsed_ms={elapsed} user_id={user}",
                        method, path, status, sw.ElapsedMilliseconds, userId);
            }

            return (status, result);
        }

        public Task<(int status, T? result)> RunAsync<T>(Func<Task<T?>> action, string method, string path, string? correlationId = null, string? userId = null)
            where T : class
            => RunInternalAsync(action,
                result =>
                {
                    if (result is null) return 400;
                    return result.GetType().Name.Contains("Reservation") ? 201 : 200;
                }, method, path, correlationId, userId);

        public Task<(int status, bool result)> RunAsync(Func<Task<bool>> action, string method, string path, string? correlationId = null, string? userId = null)
            => RunInternalAsync(action,
                ok => ok ? 200 : 400,
                method, path, correlationId, userId);
    }
}