using Microsoft.Data.Sqlite;

namespace shared;

public class BaseDatabaseConfiguration : IDatabaseConfiguration
{
    public static string SectionName => nameof(BaseDatabaseConfiguration);
    protected virtual string _dbFIlePath { get; set; } = "";
    public string DatabasePath
    {
        get
        {
            if (!Path.IsPathFullyQualified(_dbFIlePath))
            {
                var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
                var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
                return Path.GetFullPath(Path.Combine(rootDir, _dbFIlePath));
            }
            else
            {
                return _dbFIlePath;
            }
        }
        set
        {
            _dbFIlePath = value;
        }
    }

    public string ConnectionString
    {
        get
        {
            ArgumentNullException.ThrowIfNull(_dbFIlePath);

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
