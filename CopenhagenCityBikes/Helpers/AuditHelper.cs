using NLog;

namespace CopenhagenCityBikes.Helpers
{
    public static class AuditHelper
    {
        private static readonly Logger AuditLogger = LogManager.GetLogger("AUDIT");
        private static bool _initialized;

        public static void Initialize()
        {
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
            _initialized = true;
        }

        private static void Ensure()
        {
            if (!_initialized)
                throw new InvalidOperationException("AuditHelper.Initialize() must be called before logging.");
        }

        private static void LogAudit(string action, string userId, string ip, object? additional = null)
        {
            Ensure();

            var evt = new LogEventInfo(LogLevel.Info, AuditLogger.Name, "USER_ACTION");
            evt.Properties["action"] = action;
            evt.Properties["user_id"] = userId;
            evt.Properties["ip"] = ip;
            evt.Properties["log_type"] = "audit";
            evt.Properties["service"] = "city-bikes";

            if (additional != null)
            {
                foreach (var p in additional.GetType().GetProperties())
                {
                    var v = p.GetValue(additional);
                    if (v != null)
                        evt.Properties[p.Name] = v;
                }
            }

            AuditLogger.Log(evt);
        }

        public static void LogLoginSuccess(string userId, string ip) =>
            LogAudit("LOGIN_SUCCESS", userId, ip);

        public static void LogLoginFailure(string userId, string ip) =>
            LogAudit("LOGIN_FAILURE", userId, ip);

        public static void LogReservationCreate(string userId, string bikeId, string ip) =>
            LogAudit("RESERVATION_CREATE", userId, ip, new { bike_id = bikeId });

        public static void LogRentalStart(string userId, string rentalId, string ip) =>
            LogAudit("RENTAL_START", userId, ip, new { rental_id = rentalId });

        public static void LogRentalEnd(string userId, string rentalId, string ip, TimeSpan duration, decimal fees) =>
            LogAudit("RENTAL_END", userId, ip, new
            {
                rental_id = rentalId,
                duration = (long)duration.TotalMilliseconds,
                fees_charged = fees
            });

        public static void LogAdminInventoryUpdate(string adminId, int delta, string ip) =>
            LogAudit("ADMIN_INVENTORY_UPDATE", adminId, ip, new { delta });
    }
}