using Microsoft.Data.Sqlite;

namespace shared.data;

public class BaseDatabaseConfiguration : IDatabaseConfiguration
{
    public static string SectionName => throw new NotImplementedException("Section name must be overridden.");
    protected string _dbFilePath = "";
    protected virtual string DefaultPath { get; } = "";
    public virtual string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath)) _dbFilePath = DefaultPath;

            if (Path.IsPathFullyQualified(_dbFilePath))
            {
                return _dbFilePath;
            }
            else
            {
                var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
                var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
                _dbFilePath = Path.GetFullPath(Path.Combine(rootDir, _dbFilePath));
                return _dbFilePath;
            }
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
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                // Cache = SqliteCacheMode.Shared,
                Pooling = true
            };

            return builder.ToString();
        }
    }
}
