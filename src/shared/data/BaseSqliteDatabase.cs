using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace shared.data;

public class BaseSqliteDatabase : IDisposable
{
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
            return Path.GetFullPath(_config.DatabasePath);
        }
    }

    public SqliteConnection GetConnection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? throw new InvalidOperationException("Unable to determine database directory"));

        if (!System.IO.File.Exists(DatabasePath))
        {
            // Creating an empty file is enough for SQLite to initialize it on first connect
            using (System.IO.File.Create(DatabasePath)) { }
        }

        var lockObject = s_databaseLocks.GetOrAdd(DatabasePath, _ => new object());
        lock (lockObject)
        {
            var connection = new SqliteConnection(_config.ConnectionString);

            connection.Open();

            var access = FileHelper.CanAccessFile(DatabasePath, FileAccess.ReadWrite);
            if (access is not FileAccessResult.Available)
            {
                throw new FieldAccessException(FileHelper.AccessMessage(access, DatabasePath, FileAccess.ReadWrite));
            }
            // WAL mode allows one writer + multiple readers concurrently across connections.
            // busy_timeout tells SQLite retry on a locked write for up to 5 s rather than
            // immediately returning SQLITE_BUSY.
            connection.Execute("PRAGMA journal_mode=WAL;");
            connection.Execute("PRAGMA busy_timeout=5000;");

            Debug.Assert(connection.Database == "main", $"Expected main, found: {connection.Database}");

            Create();

            return connection;
        }
    }

    protected void Create()
    {
        ArgumentNullException.ThrowIfNull(CreateQueryResource);

        string query = GetQueryFromResource(QueryAssemblyType, CreateQueryResource);

        // is called from GetConnection, must use its own connection
        using var connection = new SqliteConnection(_config.ConnectionString);
        connection.Open();

        var transaction = connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction");
        try
        {
            using var command = new SqliteCommand(query, connection, transaction);
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
        using var connection = GetConnection();

        ArgumentNullException.ThrowIfNull(TruncateQueryResource);

        string query = GetQueryFromResource(QueryAssemblyType, TruncateQueryResource);

        var transaction = connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction");
        try
        {
            using var command = new SqliteCommand(query, connection, transaction);
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
