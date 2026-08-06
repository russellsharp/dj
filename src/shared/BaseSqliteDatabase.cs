using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
namespace shared;

public class BaseSqliteDatabase : IDisposable
{
    protected SqliteConnection? _connection = null;
    protected IDatabaseConfiguration? _config { get; set; } = null;
    protected static readonly ConcurrentDictionary<string, object> s_databaseLocks = new();
    protected const int _commandTimeoutMs = 2000;
    protected virtual string CreateQueryResource => throw new NotImplementedException();
    protected virtual string TruncateQueryResource => throw new NotImplementedException();
    protected virtual Type QueryAssemblyType => throw new NotImplementedException();
    public virtual string SectionName => throw new NotImplementedException();

    protected string DatabasePath
    {
        get
        {
            ArgumentNullException.ThrowIfNull(_config);
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, _config.DatabasePath));
        }
    }

    protected string ConnectionString
    {
        get
        {
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

    public void Connect()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? throw new InvalidOperationException("Unable to determine database directory"));

        if (!File.Exists(DatabasePath))
        {
            // Creating an empty file is enough for SQLite to initialize it on first connect
            using (File.Create(DatabasePath)) { }
        }

        var lockObject = s_databaseLocks.GetOrAdd(DatabasePath, _ => new object());
        lock (lockObject)
        {
            _connection = new SqliteConnection(ConnectionString);

            _connection.Open();

            var access = FileHelper.CanAccessFile(DatabasePath, FileAccess.ReadWrite);
            if (access is not FileAccessResult.Available)
            {
                throw new FieldAccessException(FileHelper.AccessMessage(access, DatabasePath, FileAccess.ReadWrite));
            }
            // WAL mode allows one writer + multiple readers concurrently across connections.
            // busy_timeout tells SQLite retry on a locked write for up to 5 s rather than
            // immediately returning SQLITE_BUSY.
            _connection.Execute("PRAGMA journal_mode=WAL;");
            _connection.Execute("PRAGMA busy_timeout=5000;");

            Debug.Assert(_connection.Database == "main", $"Expected main, found: {_connection.Database}");
            Create();
        }
    }

    public bool IsConnected()
    {
        return _connection is not null && _connection.State == ConnectionState.Open;
    }

    public void EnsureConnected()
    {
        if (_connection is null)
        {
            Connect();
            return;
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    public void Disconnect()
    {
        var lockObject = s_databaseLocks.GetOrAdd(DatabasePath, _ => new object());
        lock (lockObject)
        {
            SqliteConnection.ClearAllPools();
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
    }

    protected void Create()
    {
        ArgumentNullException.ThrowIfNull(CreateQueryResource);

        string query = GetQueryFromResource(QueryAssemblyType, CreateQueryResource);

        var transaction = _connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction");
        try
        {
            using var command = new SqliteCommand(query, _connection, transaction);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public virtual async Task Truncate()
    {
        ArgumentNullException.ThrowIfNull(TruncateQueryResource);

        string query = GetQueryFromResource(QueryAssemblyType, TruncateQueryResource);

        var transaction = _connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction");
        try
        {
            using var command = new SqliteCommand(query, _connection, transaction);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    protected static string GetQueryFromResource(Type ModuleName, string resourceName)
    {
        //find embedded resources in shared library
        var assembly = ModuleName.Assembly;

        string? query = null;

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is null) throw new ArgumentNullException(resourceName);

            using (var reader = new StreamReader(stream))
            {
                query = reader.ReadToEnd();
            }
        }
        return query!;
    }

    #region IDisposable
    private int _disposed = 0;

    public virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            if (disposing)
            {
                var conn = _connection;
                if (conn != null && conn.State == ConnectionState.Open)
                    conn?.Close();
            }

            //dispose unmanaged objects
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~BaseSqliteDatabase()
    {
        Dispose();
    }

    #endregion IDisposable    
}
