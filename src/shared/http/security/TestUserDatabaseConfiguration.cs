using Microsoft.Data.Sqlite;

namespace shared.http.security;

public class TestUserDatabaseConfiguration : IDatabaseConfiguration
{
    private static string DatabasePathKey { get; } = "DJ_USER_DATABASE_PATH";
    public static string SectionName { get; } = "AuthUsers";
    private string _dbFilePath = "";
    public string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(DatabasePathKey) ?? throw new ArgumentNullException($"Database connection string should be set in environment variable: {DatabasePathKey}");
            }

            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, _dbFilePath));
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
