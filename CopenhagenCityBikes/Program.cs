using CopenhagenCityBikes.Api.Helpers;
using CopenhagenCityBikes.Api.Models;
using CopenhagenCityBikes.Helpers;
using CopenhagenCityBikes.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using System.Net;

var logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/system-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({correlation_id}) {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

Log.Logger = logger;

Log.Information("Application startup");

AuditHelper.Initialize();

#region Simulated Operations
using IHost host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices(services =>
    {
        services.AddSingleton<BikeService>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<SimulatedRequestRunner>();
    })
    .Build();

var svc = host.Services.GetRequiredService<BikeService>();
var httpContextAccessor = host.Services.GetRequiredService<IHttpContextAccessor>();
var runner = host.Services.GetRequiredService<SimulatedRequestRunner>();

var rand = new Random();

static HttpContext CreateFakeContext(string correlationId)
{
    var ctx = new DefaultHttpContext();
    ctx.Connection.RemoteIpAddress = IPAddress.Parse($"192.168.0.{new Random().Next(2, 254)}");
    ctx.Items["CorrelationId"] = correlationId;
    return ctx;
}

var operations = new[] { "get", "reserve", "start", "end", "login", "inventory" };

var createdReservations = new List<string>();
var createdRentals = new List<string>();

// En sekvens af 30 operations hvor alle bliver lavet minimum 1 gang
var sequence = new List<string>();
sequence.AddRange(operations);
while (sequence.Count < 30)
{
    sequence.Add(operations[rand.Next(operations.Length)]);
}
sequence = sequence.OrderBy(_ => rand.Next()).ToList();

foreach (var op in sequence)
{
    var correlationId = Guid.NewGuid().ToString();
    using (LogContext.PushProperty("correlation_id", correlationId))
    {
        var ctx = CreateFakeContext(correlationId);
        httpContextAccessor.HttpContext = ctx;

        try
        {
            switch (op)
            {
                case "get":
                    var (statusGet, bikes) = await runner.RunAsync(() => svc.GetAvailableBikesAsync(), "GET", "/bikes", correlationId);
                    break;
                case "reserve":
                    var avail = (await svc.GetAvailableBikesAsync()).ToList();
                    if (avail.Count == 0)
                        break;
                    var bike = avail[rand.Next(avail.Count)];
                    var resReq = new ReserveRequest("u123", bike.Id);
                    var (statusRes, reservation) = await runner.RunAsync(() => svc.ReserveBikeAsync(resReq, ctx), "POST", "/reservations", correlationId, "u123");
                    if (reservation != null)
                        createdReservations.Add(reservation.Id);
                    break;
                case "start":
                    if (createdReservations.Count == 0)
                        break;
                    var rid = createdReservations[rand.Next(createdReservations.Count)];
                    var startReq = new StartRentalRequest("u123", rid);
                    var (statusStart, rental) = await runner.RunAsync(() => svc.StartRentalAsync(startReq, ctx), "POST", "/rentals/start", correlationId, "u123");
                    if (rental != null)
                        createdRentals.Add(rental.Id);
                    break;
                case "end":
                    if (createdRentals.Count == 0)
                        break;
                    var rentId = createdRentals[rand.Next(createdRentals.Count)];
                    var endReq = new EndRentalRequest("u123", rentId);
                    var (statusEnd, ended) = await runner.RunAsync(() => svc.EndRentalAsync(endReq, ctx), "POST", "/rentals/end", correlationId, "u123");
                    break;
                case "login":
                    var useSuccess = rand.NextDouble() < 0.8;
                    var loginReq = new LoginRequest(useSuccess ? "u123" : "unknown", useSuccess ? "password" : "bad");
                    var (statusLogin, loginOk) = await runner.RunAsync(() => svc.LoginAsync(loginReq, ctx), "POST", "/auth/login", correlationId, loginReq.UserId);
                    break;
                case "inventory":
                    var invReq = new InventoryUpdateRequest("admin1", $"b-{rand.Next(100,200)}", 1);
                    var (statusInv, invOk) = await runner.RunAsync(() => svc.UpdateInventoryAsync(invReq, ctx), "POST", "/admin/inventory", correlationId, invReq.AdminId);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Operation {op} failed", op);
        }

        await Task.Delay(rand.Next(50, 200));
    }
}
#endregion

Log.Information("Application shutdown");

Log.CloseAndFlush();

return 0;
