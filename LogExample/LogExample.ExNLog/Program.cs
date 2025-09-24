using NLog;
using NLog.Config;

class Program
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly Logger Audit = LogManager.GetLogger("AUDIT");

    static void Main(string[] args)
    {
        LogManager.Setup().LoadConfigurationFromFile("nlog.config");

        try
        {
            Log.Info("Application startup");

            var correlationId = Guid.NewGuid().ToString();
            Log.WithProperty("correlation_id", correlationId)
               .Info("Hello system log");

            Audit.WithProperty("correlation_id", correlationId)
                 .Info("USER_ACTION {action} {user_id} {ip}", "LOGIN_SUCCESS", "alice", "203.0.113.42");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception");
        }
        finally
        {
            Log.Info("Shutting down");
            LogManager.Shutdown();
        }
    }
}
