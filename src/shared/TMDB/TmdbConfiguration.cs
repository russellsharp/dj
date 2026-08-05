using Microsoft.Data.Sqlite;
namespace shared.TMDB;

public class TMDBConfiguration : IDatabaseConfiguration
{
    private static string API_KEY_KEY { get; } = "DJ_TMDB_API_KEY";
    public static string DatabasePathKey { get; } = "DJ_TMDB_DATABASE_PATH";
    public static string SectionName { get; } = "TMDB";
    public required string BaseUrl { get; init; } = "https://api.themoviedb.org/3";
    public string? ApiKey { get; set; } = GetApiKey();
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

    private string _dbFilePath { get; set; } = "testdata/tmdb.db";
    public string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(DatabasePathKey) ?? throw new ArgumentNullException($"Database path should be set in environment variable: {DatabasePathKey}");
            }
            return _dbFilePath;
        }
        set
        {
            _dbFilePath = value;
        }
    }

    public string ConnectionString
    {
        get
        {
            ArgumentNullException.ThrowIfNull(_dbFilePath);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            };

            return builder.ToString();
        }
    }
}


