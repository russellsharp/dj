using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
namespace shared.data;

public class MediaDatabaseConfiguration : IDatabaseConfiguration
{
    public const string SectionName = nameof(MediaDatabaseConfiguration);
    public const string DatabasePathKey = "DJ_MEDIA_DATABASE_PATH";
    //Not needed for all consumers
    public string DataFile
    {
        get;
        set;
    } = "data/media.db";

    public string ConnectionString
    {
        get => new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ConnectionString;
    }

    private string _dbFilePath { get; set; } = "testdata/media.db";
    public string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(DatabasePathKey) ?? throw new ArgumentNullException($"Database path should be set in environment variable: {DatabasePathKey}");
            }

            if (!Path.IsPathFullyQualified(_dbFilePath))
            {
                return _dbFilePath;
            }
            else
            {
                var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
                var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
                return Path.Combine(rootDir, _dbFilePath);
            }
        }
        set
        {
            _dbFilePath = value;
        }
    }
}
