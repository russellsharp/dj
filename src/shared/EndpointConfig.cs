using shared.TMDB;
namespace shared;

public class EndpointConfig
{
    public string SectionName { get; } = nameof(EndpointConfig);
    public required string BaseUrl { get; init; } = "https://api.themoviedb.org/3";
    public string ApiKey { get; init; } = Repo.SUPER_SECRET_API_KEY;
    public string DatabasePath { get; init; } = "testdata/tmdb.db";
    public int RequestLimit { init; get; } = 40;
    public int RequestWindowSeconds { init; get; } = 10;
    public int AttemptCountMax { init; get; } = 10;
    public int BackOffTimeMs { init; get; } = 1000;
}
