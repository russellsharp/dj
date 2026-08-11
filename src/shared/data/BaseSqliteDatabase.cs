using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Diagnostics;
using System.Collections.Concurrent;
using shared.utility;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace shared.data;

public class BaseSqliteDatabase : IDisposable
{
    protected IDatabaseConfiguration? _config { get; set; } = null;
    protected static readonly ConcurrentDictionary<string, Lock> s_databaseLocks = new();
    protected Lock GetLock()
    {
        return s_databaseLocks.GetOrAdd(DatabasePath, _ => new Lock());
    }
    protected const int _commandTimeoutMs = 2000;
    protected virtual string CreateQueryResource => throw new NotImplementedException();
    protected virtual string TruncateQueryResource => throw new NotImplementedException();
    protected virtual Type QueryAssemblyType => throw new NotImplementedException();
    public virtual string SectionName => throw new NotImplementedException();
    protected string DatabasePath => Path.GetFullPath(_config.DatabasePath);
    protected SqliteConnection? _connection = null;

    [MemberNotNull(nameof(_connection))]
    protected void EnsureConnected()
    {
        if (_connection is null)
        {
            _connection = new SqliteConnection(_config.ConnectionString);
        }

        if (_connection.State == ConnectionState.Open) return;

        var logger = new LoggerFactory().CreateLogger<BaseSqliteDatabase>();
        logger.LogInformation($"Creating database directory: {PathUtilities.GetDirectory(DatabasePath)}");

        Directory.CreateDirectory(PathUtilities.GetDirectory(DatabasePath) ?? throw new InvalidOperationException("Unable to determine database directory"));

        if (!System.IO.File.Exists(DatabasePath))
        {
            // Creating an empty file is enough for SQLite to initialize it on first connect
            using (System.IO.File.Create(DatabasePath)) { }
        }

        var access = FileHelper.CanAccessFile(DatabasePath, FileAccess.ReadWrite);
        if (access is not FileAccessResult.Available)
        {
            throw new FieldAccessException(FileHelper.AccessMessage(access, DatabasePath, FileAccess.ReadWrite));
        }

        lock (GetLock())
        {
            try
            {
                _connection.Open();
                _connection.Execute("PRAGMA busy_timeout=5000;");

                Debug.Assert(_connection.Database == "main", $"Expected main, found: {_connection.Database}");

                Create();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while connecting to database: {ex}");
                _connection.Close();
                throw;
            }
        }
    }

    protected void Create()
    {
        ArgumentNullException.ThrowIfNull(CreateQueryResource);

        EnsureConnected();

        lock (GetLock())
        {
            string query = GetQueryFromResource(QueryAssemblyType, CreateQueryResource);

            var transaction = _connection!.BeginTransaction(IsolationLevel.Serializable);
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
    }

    public virtual async Task Truncate()
    {
        EnsureConnected();

        lock (GetLock())
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
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        if (disposing)
        {
            // dispose managed objects
        }
        _connection?.Dispose();
        _connection = null;

        // force sqlite to close all connections and file references
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();

        //dispose unmanaged objects
    }

    public void Dispose()
    {
        Dispose(true);
    }

    ~BaseSqliteDatabase()
    {
        Dispose();
    }

    #endregion IDisposable    
}
