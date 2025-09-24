using Serilog;
using Serilog.Formatting.Elasticsearch;

namespace CopenhagenCityBikes.Helpers
{
    public static class AuditHelper
    {
        private static ILogger? _auditLogger;

        public static void Initialize()
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var auditPath = Path.Combine(logDir, "audit-.log");

            _auditLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(
                    formatter: new ElasticsearchJsonFormatter(renderMessage: true),
                    path: auditPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90,
                    shared: true)
                .CreateLogger();
        }

        private static ILogger ForAuditContext(string action, string userId, string ip, object? additional = null)
        {
            if (_auditLogger is null)
                throw new InvalidOperationException("AuditHelper.Initialize() must be called before logging.");

            var ctx = _auditLogger
                .ForContext("logger_name", "AUDIT")
                .ForContext("action", action)
                .ForContext("user_id", userId)
                .ForContext("ip", ip)
                .ForContext("log_type", "audit")
                .ForContext("service", "city-bikes");

            if (additional != null)
            {
                foreach (var prop in additional.GetType().GetProperties())
                {
                    ctx = ctx.ForContext(prop.Name, prop.GetValue(additional));
                }
            }

            return ctx;
        }

        public static void LogLoginSuccess(string userId, string ip) =>
            ForAuditContext("LOGIN_SUCCESS", userId, ip).Information("USER_ACTION");

        public static void LogLoginFailure(string userId, string ip) =>
            ForAuditContext("LOGIN_FAILURE", userId, ip).Information("USER_ACTION");

        public static void LogReservationCreate(string userId, string bikeId, string ip) =>
            ForAuditContext("RESERVATION_CREATE", userId, ip, new { bike_id = bikeId }).Information("USER_ACTION");

        public static void LogRentalStart(string userId, string rentalId, string ip) =>
            ForAuditContext("RENTAL_START", userId, ip, new { resource_id = $"rental:{rentalId}" }).Information("USER_ACTION");

        public static void LogRentalEnd(string userId, string rentalId, string ip, TimeSpan duration, decimal fees) =>
            ForAuditContext("RENTAL_END", userId, ip, new { resource_id = $"rental:{rentalId}", duration_seconds = (int)duration.TotalSeconds, fees_charged = fees }).Information("USER_ACTION");

        public static void LogAdminInventoryUpdate(string adminId, int delta, string ip) =>
            ForAuditContext("ADMIN_INVENTORY_UPDATE", adminId, ip, new { delta }).Information("USER_ACTION");
    }
}