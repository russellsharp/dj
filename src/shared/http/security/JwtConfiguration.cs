namespace shared;

public class JwtConfiguration
{
    public static string SectionName = "Jwt";
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
    public static string DJ_JWT_ISSUER { get; } = "DJ_JWT_ISSUER";
    public static string DJ_JWT_AUDIENCE { get; } = "DJ_JWT_AUDIENCE";
}