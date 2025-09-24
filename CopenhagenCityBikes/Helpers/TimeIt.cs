using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CopenhagenCityBikes.Helpers
{
    public static class TimeIt
    {
        public static async Task TimeItAsync(ILogger logger, LogLevel level, string description, Func<Task> action)
        {
            if (!logger.IsEnabled(level))
            {
                await action();
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await action();
            }
            finally
            {
                sw.Stop();
                logger.Log(level, "{description} elapsed_ms={elapsed}", description, sw.ElapsedMilliseconds);
            }
        }
    }
}