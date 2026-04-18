namespace Platform.WebApi.Configuration;

internal static class PlatformFeatureFlags
{
    internal static bool IsMetricsEnabled(IConfiguration configuration) =>
        ParseBool(configuration["FeatureFlags:MetricsEnabled"], defaultValue: true);

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var b))
        {
            return b;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return defaultValue;
    }
}
