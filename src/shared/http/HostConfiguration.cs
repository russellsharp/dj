using shared.http;

namespace shared;

public class HostConfiguration
{
    public static string SectionName = typeof(HostConfiguration).Name;
    public static string DJ_HOST_ALLOWED_CORS_URL = "DJ_HOST_ALLOWED_CORS_URL";
    public List<string> CorsAllowedUrl { get; init; } = ["127.0.0.1"];
    public Dictionary<string, RateLimiterConfiguration> RateLimiters { get; init; } = new();
    public JwtConfiguration Jwt { get; init; } = new();
}
