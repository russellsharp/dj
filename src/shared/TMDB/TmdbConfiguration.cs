using Microsoft.Data.Sqlite;
using shared.data;
namespace shared.TMDB;

public class TMDBConfiguration : BaseDatabaseConfiguration
{
    public new static string SectionName { get; } = "TMDB";
    public required string BaseUrl { get; init; } = "https://api.themoviedb.org/3";
    public string? ApiKey { get; set; } = GetApiKey();
    public int TitleWeight { init; get; } = 100;
    public int OverviewWeight { init; get; } = 1;
    public bool IncludeAdult { init; get; } = false;

    #region Database configuration
    private static string API_KEY_KEY { get; } = "DJ_TMDB_API_KEY";
    public static string DatabasePathKey { get; } = "DJ_TMDB_DATABASE_PATH";
    public static string? GetApiKey()
    {
        return Environment.GetEnvironmentVariable(API_KEY_KEY);
    }
    protected override string DefaultPath { get; } = "testdata/tmdb.db";

    public override string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(DatabasePathKey) ?? DefaultPath;
            }
            return base.DatabasePath;
        }
        set
        {
            _dbFilePath = value;
        }
    }
    #endregion Database configuration
}


