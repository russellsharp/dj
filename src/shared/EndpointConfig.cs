using Polly;
using shared.TMDB;
namespace shared;

public interface IDbConfiguration
{
    public static string SectionName { get; }

    public static string DatabasePathKey { get; }
    public string DatabasePath { get; init; }
}

public class TMDBConfiguration : IDbConfiguration
{
    private static string API_KEY_KEY { get; } = "DJ_TMDB_API_KEY";
    public static string TMDB_DATABASE_PATH { get; } = "DJ_TMDB_DATABASE_PATH";
    public static string SectionName { get; } = "TMDB";
    public required string BaseUrl { get; init; } = "https://api.themoviedb.org/3";
    public string? ApiKey { get; set; } = GetApiKey();
    public string DatabasePath { get; init; } = "testdata/tmdb.db";
    public int RequestLimit { init; get; } = 40;
    public int RequestWindowSeconds { init; get; } = 10;
    public int RequestBurstMax { init; get; } = 1;
    public int AttemptCountMax { init; get; } = 10;
    public int BackOffTimeMs { init; get; } = 1000;
    public int TitleWeight { init; get; } = 100;
    public int OverviewWeight { init; get; } = 1;
    public bool IncludeAdult { init; get; } = false;

    public static string? GetApiKey()
    {
        return Environment.GetEnvironmentVariable(API_KEY_KEY);
    }
}


