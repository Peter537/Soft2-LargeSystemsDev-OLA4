using System.Net;
using CopenhagenCityBikes.Api.Helpers;
using CopenhagenCityBikes.Api.Models; // For ReserveRequest
using CopenhagenCityBikes.Helpers;
using CopenhagenCityBikes.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

var bootstrapLogger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config", optional: false)
    .GetCurrentClassLogger();

try
{
    bootstrapLogger.Info("Application startup");

    AuditHelper.Initialize();

    using IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            logging.AddNLog(); // Bridge Microsoft ILogger<T> to NLog
        })
        .ConfigureServices(services =>
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<BikeService>();
            services.AddSingleton<SimulatedRequestRunner>();
        })
        .Build();

    var svc = host.Services.GetRequiredService<BikeService>();
    var httpContextAccessor = host.Services.GetRequiredService<IHttpContextAccessor>();
    var runner = host.Services.GetRequiredService<SimulatedRequestRunner>();
    var logger = LogManager.GetCurrentClassLogger();

    var rand = new Random();

    static HttpContext CreateFakeContext(string correlationId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse($"192.168.0.{Random.Shared.Next(2, 254)}");
        ctx.Items["CorrelationId"] = correlationId;
        return ctx;
    }

    var operations = new[] { "get", "reserve", "start", "end", "login", "inventory" };
    var createdReservations = new List<string>();
    var createdRentals = new List<string>();

    // Build a sequence that guarantees each op at least once, then fill to 30 and shuffle
    var sequence = new List<string>(operations);
    while (sequence.Count < 30)
        sequence.Add(operations[rand.Next(operations.Length)]);
    sequence = sequence.OrderBy(_ => rand.Next()).ToList();

    foreach (var op in sequence)
    {
        var correlationId = Guid.NewGuid().ToString();
        NLog.MappedDiagnosticsLogicalContext.Set("correlation_id", correlationId);
        try
        {
            var ctx = CreateFakeContext(correlationId);
            httpContextAccessor.HttpContext = ctx;

            switch (op)
            {
                case "get":
                    {
                        var getResult = await runner.RunAsync(
                            () => svc.GetAvailableBikesAsync(),
                            "GET", "/bikes", correlationId);
                        // getResult.result is List<Bike>
                        break;
                    }

                case "reserve":
                    {
                        var bikes = await svc.GetAvailableBikesAsync();
                        var avail = bikes.Where(b => b.Available).ToList();
                        if (avail.Count == 0)
                            break;

                        var bike = avail[rand.Next(avail.Count)];
                        var req = new ReserveRequest("u123", bike.Id);

                        var reserveResult = await runner.RunAsync(
                            () => svc.ReserveBikeAsync(req, ctx),
                            "POST", "/reservations", correlationId, "u123");

                        if (reserveResult.result != null)
                            createdReservations.Add(reserveResult.result.Id);

                        break;
                    }

                case "start":
                    {
                        if (createdReservations.Count == 0)
                            break;

                        var reservationId = createdReservations[rand.Next(createdReservations.Count)];

                        var startResult = await runner.RunAsync(
                            () => svc.StartRentalAsync(reservationId, "u123", ctx),
                            "POST", "/rentals/start", correlationId, "u123");

                        var rental = startResult.result;
                        if (rental != null && !createdRentals.Contains(rental.Id))
                            createdRentals.Add(rental.Id);

                        break;
                    }

                case "end":
                    {
                        if (createdRentals.Count == 0)
                            break;

                        var rentalId = createdRentals[rand.Next(createdRentals.Count)];

                        var endResult = await runner.RunAsync(
                            () => svc.EndRentalAsync(rentalId, "u123", ctx),
                            "POST", "/rentals/end", correlationId, "u123");

                        // endResult.result is bool (true success)
                        break;
                    }

                case "login":
                    {
                        // Simulate one success + one failure each loop
                        var firstSuccess = rand.NextDouble() < 0.7;

                        var login1 = await runner.RunAsync(
                            () => svc.LoginAsync("u123", firstSuccess ? "password" : "wrong", ctx),
                            "POST", "/login", correlationId, "u123");

                        var login2 = await runner.RunAsync(
                            () => svc.LoginAsync("u123", firstSuccess ? "wrong" : "password", ctx),
                            "POST", "/login", correlationId, "u123");

                        break;
                    }

                case "inventory":
                    {
                        // Adjust inventory (admin). Negative removes availability, positive adds new bikes (per your implementation)
                        var delta = rand.Next(-2, 4);

                        var invResult = await runner.RunAsync(
                            () => svc.AdjustInventoryAsync("admin1", delta, ctx),
                            "POST", "/inventory", correlationId, "admin1");

                        break;
                    }

                default:
                    logger.Warn("Unknown simulated operation op={op}", op);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unhandled exception in operation loop op={op}", op);
        }
        finally
        {
            NLog.MappedDiagnosticsLogicalContext.Remove("correlation_id");
        }
    }

    bootstrapLogger.Info("Application shutdown");
}
catch (Exception ex)
{
    bootstrapLogger.Error(ex, "Fatal exception during startup");
    throw;
}
finally
{
    LogManager.Shutdown();
}