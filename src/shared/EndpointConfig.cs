using Polly;
using shared.TMDB;
namespace shared;

public class EndpointConfig
{
    private static string API_KEY_KEY = "TMDB_API_KEY";

    public string SectionName { get; } = nameof(EndpointConfig);
    public required string BaseUrl { get; init; } = "https://api.themoviedb.org/3";
    public string ApiKey { get; set; } = GetApiKey();
    public string DatabasePath { get; init; } = "testdata/tmdb.db";
    public int RequestLimit { init; get; } = 40;
    public int RequestWindowSeconds { init; get; } = 10;
    public int RequestBurstMax { init; get; } = 1;
    public int AttemptCountMax { init; get; } = 10;
    public int BackOffTimeMs { init; get; } = 1000;
    public int TitleWeight { init; get; } = 100;
    public int OverviewWeight { init; get; } = 1;
    public bool IncludeAdult { init; get; } = false;

    public static string GetApiKey()
    {
        return Environment.GetEnvironmentVariable(API_KEY_KEY);
    }
}


