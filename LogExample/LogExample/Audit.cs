using Serilog;

public static class Audit
{
    private static ILogger Base =>
        Log.ForContext("logger_name", "AUDIT")
           .ForContext("log_type", "audit")
           .ForContext("service", "city-bikes");

    public static void LogLoginSuccess(string userId, string ip) =>
        Base.ForContext("action", "LOGIN_SUCCESS")
            .ForContext("user_id", userId)
            .ForContext("ip", ip)
            .Information("USER_ACTION");

    public static void LogLoginFailure(string userId, string ip) =>
        Base.ForContext("action", "LOGIN_FAILURE")
            .ForContext("user_id", userId)
            .ForContext("ip", ip)
            .Information("USER_ACTION");
}
