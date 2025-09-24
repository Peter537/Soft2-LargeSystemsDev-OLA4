using Microsoft.Extensions.Configuration;
using Serilog;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Application startup");

    // Example system log
    Log.ForContext("correlation_id", Guid.NewGuid()).Information("Hello system log");

    // Example audit log
    Audit.LogLoginSuccess("alice", "203.0.113.42");

    // your app code...
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
