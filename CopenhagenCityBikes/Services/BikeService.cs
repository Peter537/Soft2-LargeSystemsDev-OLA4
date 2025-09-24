using CopenhagenCityBikes.Api.Models;
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

        public async Task<Reservation?> ReserveBikeAsync(ReserveRequest req, HttpContext ctx)
        {
            var bike = _bikes.FirstOrDefault(b => b.Id == req.BikeId && b.Available);
            if (bike == null)
            {
                _logger.LogError("Reservation failed bike_id={bike_id} error=\"SoldOutException\"", req.BikeId);
                return null;
            }

            try
            {
                await TimeIt.TimeItAsync(_logger, LogLevel.Information, "Payment service call", async () =>
                {
                    var delay = Random.Shared.Next(100, 1000);
                    await Task.Delay(delay);
                    if (delay > 500)
                    {
                        _logger.LogWarning("Payment service slow elapsed_ms={delay} using_fallback=false", delay);
                    }
                    if (Random.Shared.NextDouble() < 0.1)
                    {
                        throw new Exception("Payment failed");
                    }
                    _logger.LogInformation("External call simulation success latency={delay}", delay);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reservation failed due to payment error");
                return null;
            }

            bike.Available = false;
            var resId = Guid.NewGuid().ToString()[0..8];
            var res = new Reservation { Id = resId, UserId = req.UserId, BikeId = req.BikeId, StartTime = DateTime.UtcNow };
            _reservations[resId] = res;

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogReservationCreate(req.UserId, req.BikeId, ip);

            _logger.LogInformation("Reservation created reservation_id={reservation_id} user_id={user_id} bike_id={bike_id}", res.Id, req.UserId, req.BikeId);

            return res;
        }

        public async Task<Rental?> StartRentalAsync(StartRentalRequest req, HttpContext ctx)
        {
            if (!_reservations.TryGetValue(req.ReservationId, out var res) || res.UserId != req.UserId)
            {
                _logger.LogError("Start rental failed reservation_id={reservation_id} error=\"InvalidReservation\"", req.ReservationId);
                return null;
            }

            try
            {
                await TimeIt.TimeItAsync(_logger, LogLevel.Debug, "Verification service", async () =>
                {
                    await Task.Delay(Random.Shared.Next(50, 200));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification failed");
                return null;
            }

            var rentalId = Guid.NewGuid().ToString()[0..8];
            var rental = new Rental { Id = rentalId, ReservationId = req.ReservationId, StartTime = DateTime.UtcNow };
            _rentals[rentalId] = rental;

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogRentalStart(req.UserId, rentalId, ip);

            _logger.LogInformation("Rental started rental_id={rental_id} reservation_id={reservation_id} user_id={user_id}", rental.Id, req.ReservationId, req.UserId);

            return rental;
        }

        public async Task<Rental?> EndRentalAsync(EndRentalRequest req, HttpContext ctx)
        {
            if (!_rentals.TryGetValue(req.RentalId, out var rental) || !_reservations.TryGetValue(rental.ReservationId, out var res) || res.UserId != req.UserId)
            {
                _logger.LogError("End rental failed rental_id={rental_id} error=\"InvalidRental\"", req.RentalId);
                return null;
            }

            rental.EndTime = DateTime.UtcNow;
            rental.Duration = rental.EndTime - rental.StartTime;
            rental.Fees = (decimal)rental.Duration.Value.TotalHours * 10m; // Assume $10/hour

            var bike = _bikes.First(b => b.Id == res.BikeId);
            bike.Available = true;

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogRentalEnd(req.UserId, req.RentalId, ip, rental.Duration.Value, rental.Fees.Value);

            _logger.LogInformation("Rental ended rental_id={rental_id} duration_ms={duration} fees={fees}", rental.Id, (int)rental.Duration.Value.TotalMilliseconds, rental.Fees.Value);

            return rental;
        }

        public Task<bool> LoginAsync(LoginRequest req, HttpContext ctx)
        {
            var success = _users.TryGetValue(req.UserId, out var storedPass) && storedPass == req.Password; // Stub, no hash for sim
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (success)
            {
                AuditHelper.LogLoginSuccess(req.UserId, ip);
                _logger.LogInformation("Login success user_id={user_id}", req.UserId);
                return Task.FromResult(true);
            }
            else
            {
                _logger.LogWarning("Login failure attempt for user_id={user_id}", req.UserId);
                AuditHelper.LogLoginFailure(req.UserId, ip);
                return Task.FromResult(false);
            }
        }

        public Task<bool> UpdateInventoryAsync(InventoryUpdateRequest req, HttpContext ctx)
        {
            if (!_users.ContainsKey(req.AdminId))
            {
                _logger.LogError("Inventory update failed admin_id={admin_id} error=\"Unauthorized\"", req.AdminId);
                return Task.FromResult(false);
            }

            var bike = _bikes.FirstOrDefault(b => b.Id == req.BikeId);
            if (bike != null)
            {
                bike.Available = req.Delta > 0;
            }
            else
            {
                if (req.Delta > 0)
                {
                    _bikes.Add(new Bike { Id = req.BikeId, Available = true });
                }
                else
                {
                    _logger.LogWarning("Cannot remove non-existent bike bike_id={bike_id}", req.BikeId);
                    return Task.FromResult(false);
                }
            }

            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            AuditHelper.LogAdminInventoryUpdate(req.AdminId, req.Delta, ip);

            _logger.LogInformation("Inventory updated admin_id={admin_id} bike_id={bike_id} delta={delta}", req.AdminId, req.BikeId, req.Delta);

            return Task.FromResult(true);
        }
    }
}