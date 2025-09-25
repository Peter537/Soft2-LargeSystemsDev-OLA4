using CopenhagenCityBikes.Helpers;
using CopenhagenCityBikes.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CopenhagenCityBikes.Services
{
    public class BikeService
    {
        private readonly ILogger<BikeService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly List<Bike> _bikes = new()
        {
            new Bike { Id = "b-42", Available = true },
            new Bike { Id = "b-43", Available = true },
            new Bike { Id = "b-44", Available = false }
        };

        private static readonly Dictionary<string, Reservation> _reservations = new();
        private static readonly Dictionary<string, Rental> _rentals = new();

        private static readonly Dictionary<string, string> _users = new()
        {
            { "u123", "password" },
            { "admin1", "adminpass" }
        };

        public BikeService(ILogger<BikeService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<List<Bike>> GetAvailableBikesAsync()
        {
            var available = _bikes.Where(b => b.Available).ToList();
            if (available.Count < 3)
            {
                _logger.LogWarning("Low inventory count={count}", available.Count);
            }

            _logger.LogInformation("GET /bikes returned {count} bikes", available.Count);

            return Task.FromResult(available);
        }

        public async Task<Reservation?> ReserveBikeAsync(ReservationRequest req, HttpContext ctx)
        {
            var bike = _bikes.FirstOrDefault(b => b.Id == req.BikeId);
            if (bike == null || !bike.Available)
            {
                _logger.LogError("Reservation failed bike_id={bikeId} reason={reason}", req.BikeId, bike == null ? "NOT_FOUND" : "UNAVAILABLE");
                return null;
            }

            // Simulate external call timing
            await Helpers.TimeIt.TimeItAsync(_logger, LogLevel.Information, "External payment verification", async () =>
            {
                await Task.Delay(Random.Shared.Next(50, 150));
                _logger.LogInformation("External call simulation success latency={delay}", "ok");
            });

            bike.Available = false;
            var resId = Guid.NewGuid().ToString()[0..8];
            var res = new Reservation
            {
                Id = resId,
                UserId = req.UserId,
                BikeId = req.BikeId,
                StartTime = DateTime.UtcNow
            };
            _reservations[resId] = res;

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogReservationCreate(req.UserId, req.BikeId, ip);
            return res;
        }

        public Task<Rental?> StartRentalAsync(string reservationId, string userId, HttpContext ctx)
        {
            if (!_reservations.TryGetValue(reservationId, out var res) || res.UserId != userId)
            {
                _logger.LogError("Rental start failed reservation_id={resId} reason={reason}", reservationId, "NOT_FOUND_OR_UNAUTHORIZED");
                return Task.FromResult<Rental?>(null);
            }

            var rental = new Rental
            {
                Id = Guid.NewGuid().ToString()[0..8],
                UserId = userId,
                BikeId = res.BikeId,
                StartTime = DateTime.UtcNow
            };
            _rentals[rental.Id] = rental;
            _reservations.Remove(reservationId);

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogRentalStart(userId, rental.Id, ip);

            _logger.LogInformation("Rental started rental_id={id} bike_id={bike}", rental.Id, rental.BikeId);
            return Task.FromResult<Rental?>(rental);
        }

        public Task<bool> EndRentalAsync(string rentalId, string userId, HttpContext ctx)
        {
            if (!_rentals.TryGetValue(rentalId, out var rental) || rental.UserId != userId)
            {
                _logger.LogError("Rental end failed rental_id={id} reason={reason}", rentalId, "NOT_FOUND_OR_UNAUTHORIZED");
                return Task.FromResult(false);
            }

            rental.EndTime = DateTime.UtcNow;

            var duration = rental.EndTime.Value - rental.StartTime;
            var fees = Math.Round((decimal)duration.TotalMinutes * 0.5m, 2);

            var bike = _bikes.First(b => b.Id == rental.BikeId);
            bike.Available = true;

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogRentalEnd(userId, rental.Id, ip, duration, fees);

            _logger.LogInformation("Rental ended rental_id={id} duration_ms={dur} fees={fees}",
                rental.Id, (long)duration.TotalMilliseconds, fees);

            return Task.FromResult(true);
        }

        public Task<bool> LoginAsync(string userId, string password, HttpContext ctx)
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ok = _users.TryGetValue(userId, out var pw) && pw == password;
            if (ok)
            {
                AuditHelper.LogLoginSuccess(userId, ip);
                _logger.LogInformation("Login success user_id={user}", userId);
            }
            else
            {
                AuditHelper.LogLoginFailure(userId, ip);
                _logger.LogWarning("Login failure user_id={user}", userId);
            }
            return Task.FromResult(ok);
        }

        public Task<bool> AdjustInventoryAsync(string adminId, int delta, HttpContext ctx)
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!_users.ContainsKey(adminId) || !adminId.StartsWith("admin"))
            {
                _logger.LogWarning("Inventory update denied admin_id={admin}", adminId);
                return Task.FromResult(false);
            }

            if (delta > 0)
            {
                for (int i = 0; i < delta; i++)
                {
                    _bikes.Add(new Bike { Id = $"b-new-{Guid.NewGuid().ToString()[0..4]}", Available = true });
                }
            }
            else if (delta < 0)
            {
                var toRemove = _bikes.Where(b => b.Available).Take(Math.Abs(delta)).ToList();
                foreach (var b in toRemove)
                    _bikes.Remove(b);
            }

            AuditHelper.LogAdminInventoryUpdate(adminId, delta, ip);
            _logger.LogInformation("Inventory adjusted admin_id={admin} delta={delta}", adminId, delta);
            return Task.FromResult(true);
        }
    }
}