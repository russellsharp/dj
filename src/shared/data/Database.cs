using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using SQLitePCL;
using System.Transactions;
using Dapper;
using Dapper.Contrib.Extensions;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml.Linq;
using Dapper.Logging;


namespace shared.data;

using System;
using System.Data;
using System.Data.Common;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

public interface IDatabase
{
    void Connect();
    void Create();
    void Disconnect();
    void Dispose(bool disposing);
    void Dispose();
    void EnsureConnected();
    Task<File?> File(string path);
    Task<bool> FileExists(string filePath);
    Task<IEnumerable<File>> Files();
    Task<IEnumerable<File>> Files(IEnumerable<string> paths);
    Task<IEnumerable<File>> FilesByDirectory(IEnumerable<string> paths);
    Task<IEnumerable<File>> FilesByExtensions(IEnumerable<string> extensions);
    Task Insert(File file);
    Task InsertOrUpdate(IEnumerable<File> testData);
    bool IsConnected();
    Task Truncate();
}

public class Database : IDisposable, IDatabase
{
    private static readonly ConcurrentDictionary<string, object> s_databaseLocks = new();
    private readonly DatabaseConfiguration _config;
    private SqliteConnection? _connection = null;
    private int _commandTimeoutSeconds = 20;

    public Database(IOptions<DatabaseConfiguration> config)
    {
        _config = config.Value;

        SqlMapper.AddTypeHandler(new UtcDateTimeHandler());
    }

    /// <summary>
    /// Connects to the database file.  Will create the file and directory path if necessary.
    /// </summary>
    public void Connect()
    {
        var rootDir = Path.GetDirectoryName(Environment.ProcessPath);

#pragma warning disable CS8604 // Possible null reference argument.
        var dbPath = Path.GetFullPath(Path.Combine(rootDir, _config.DataFile));
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
#pragma warning restore CS8604 // Possible null reference argument.

        var lockObject = s_databaseLocks.GetOrAdd(dbPath, _ => new object());
        lock (lockObject)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            _connection = new SqliteConnection(builder.ConnectionString);

            _connection.Open();

            // WAL mode allows one writer + multiple readers concurrently across connections.
            // busy_timeout tells SQLite retry on a locked write for up to 5 s rather than
            // // immediately returning SQLITE_BUSY.
            _connection.Execute("PRAGMA journal_mode=WAL;");
            _connection.Execute("PRAGMA busy_timeout=5000;");

            Debug.Assert(_connection.Database == "main", $"Expected main, found: {_connection.Database}");
            Create();
        }
    }

    public void Disconnect()
    {
        var rootDir = Path.GetDirectoryName(Environment.ProcessPath);

#pragma warning disable CS8604 // Possible null reference argument.
        var dbPath = Path.GetFullPath(Path.Combine(rootDir, _config.DataFile));
#pragma warning restore CS8604 // Possible null reference argument.

        var lockObject = s_databaseLocks.GetOrAdd(dbPath, _ => new object());
        lock (lockObject)
        {
            SqliteConnection.ClearAllPools();
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
    }

    public void Create()
    {
        string query = GetQueryFromResource(QueryFiles.CreateDatabase);

        using (var transaction = _connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction"))
        {
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

    public async Task Insert(File file)
    {
        try
        {
            using var transaction = _connection.BeginTransaction();
            const string sql = @"
            INSERT INTO file (
                path_hash, path, data_modified, date_created, 
                size, extension, hash, attributes, extra_attributes
            ) VALUES (
                @path_hash, @path, @date_modified, @date_created, 
                @size, @extension, @hash, @attributes, @extra_attributes
            );";

            try
            {
                var parameters = new
                {
                    file.path_hash,
                    file.path,
                    // SQLite automatically handles ISO8601 string dates seamlessly
                    data_modified = file.date_modified.ToIsoUtcString(),
                    date_created = file.date_created.ToIsoUtcString(),
                    file.size,
                    file.extension,
                    file.hash,
                    file.attributes,
                    file.extra_attributes
                };

                await _connection.ExecuteAsync(sql, parameters, transaction, _commandTimeoutSeconds, CommandType.Text);
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while inserting file record: {ex}");
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WHAT {ex}");
        }
    }

    public async Task InsertOrUpdate(IEnumerable<File> testData)
    {
        try
        {
            using var transaction = await _connection.BeginTransactionAsync();

            const string sql = @"
            INSERT INTO file (
                path_hash, path, date_modified, date_created, 
                size, extension, hash, attributes, extra_attributes
            ) VALUES (
                @path_hash, @path, @date_modified, @date_created, 
                @size, @extension, @hash, @attributes, @extra_attributes
            )
            ON CONFLICT(path_hash) DO UPDATE SET
                date_modified = excluded.date_modified,
                date_created = excluded.date_created,
                size = excluded.size,
                extension = excluded.extension,
                hash = excluded.hash,
                attributes = excluded.attributes,
                extra_attributes = excluded.extra_attributes;";

            var batchedParameters = testData.Select(file => new
            {
                path_hash = file.path_hash,
                path = file.path,
                date_modified = file.date_modified.ToString("o"),
                date_created = file.date_created.ToString("o"),
                size = file.size,
                extension = file.extension,
                hash = file.hash,
                attributes = file.attributes,
                extra_attributes = file.extra_attributes
            });

            try
            {
                await _connection.ExecuteAsync(sql, batchedParameters, transaction, _commandTimeoutSeconds, CommandType.Text);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while inserting file record: {ex}");
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WHAT {ex}");
        }
    }

    public async Task<bool> FileExists(string filePath)
    {
        const string sql = @"SELECT EXISTS (SELECT 1 FROM file WHERE path_hash = @path_hash)";
        try
        {
            var path_hash = FileHelper.HashString(filePath);
            return await _connection.ExecuteScalarAsync<bool>(sql, new { path_hash = path_hash });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception while checking for file entry: {ex}");
            throw;
        }
    }

    public async Task<shared.data.File?> File(string path)
    {
        const string sql = @"SELECT * FROM file WHERE path_hash = @path_hash;";

        shared.data.File? file = null;

        try
        {
            var path_hash = FileHelper.HashString(path);
            file = await _connection.QueryFirstOrDefaultAsync<shared.data.File>(sql, new { path_hash = path_hash });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while querying file record: {ex}");
            throw;
        }

        return file;
    }

    public async Task<IEnumerable<shared.data.File>> Files()
    {
        const string sql = @"SELECT * FROM file;";

        var files = Enumerable.Empty<shared.data.File>();

        try
        {
            files = await _connection.QueryAsync<shared.data.File>(sql, null, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while querying file record: {ex}");
            throw;
        }

        return files;
    }


    public async Task<IEnumerable<shared.data.File>> Files(IEnumerable<string> paths)
    {
        const string sql = @"SELECT * FROM file WHERE path_hash IN @path_hashes;";

        var files = Enumerable.Empty<shared.data.File>();

        try
        {
            var path_hashes = paths.Select(x => FileHelper.HashString(x));
            files = await _connection.QueryAsync<shared.data.File>(sql, new { path_hashes = path_hashes }, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while querying file record: {ex}");
            throw;
        }

        return files;
    }

    public async Task<IEnumerable<shared.data.File>> FilesByExtensions(IEnumerable<string> extensions)
    {
        const string sql = @"SELECT * FROM file WHERE extension IN @file_extensions;";

        var files = Enumerable.Empty<shared.data.File>();

        try
        {
            files = await _connection.QueryAsync<shared.data.File>(sql, new { file_extensions = extensions }, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while querying file record: {ex}");
            throw;
        }

        return files;
    }

    public async Task<IEnumerable<shared.data.File>> FilesByDirectory(IEnumerable<string> paths)
    {
        var sql = new StringBuilder(@"SELECT * FROM file WHERE ");

        var conditions = new List<string>();
        var parameters = new DynamicParameters();
        var searchTerms = paths.Select(x => Path.GetDirectoryName(x)).ToList();
        Console.WriteLine(string.Join(", ", searchTerms));

        for (int i = 0; i < paths.Count(); i++)
        {
            var paramName = $"@term{i}";

            conditions.Add($"path LIKE {paramName}");

            parameters.Add(paramName, $"{searchTerms[i]}%");
        }

        sql.Append(string.Join(" OR ", conditions));

        var files = Enumerable.Empty<shared.data.File>();

        try
        {
            files = await _connection.QueryAsync<shared.data.File>(sql.ToString(), parameters, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error while querying file record: {ex}");
            throw;
        }

        return files;
    }

    public async Task Truncate()
    {
        string query = GetQueryFromResource(QueryFiles.TruncateDatabase);
        using (var transaction = _connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction"))
        {
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

    private string GetQueryFromResource(string resourceName)
    {
        //find embedded resources in shared library
        var assembly = typeof(shared.data.Database).Assembly;

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
                _connection?.Close();
            }

            //dispose unmanaged objects
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Database()
    {
        Dispose();
    }

    #endregion IDisposable
}

public static class DateTimeExtensions
{
    public static string ToIsoUtcString(this DateTime value)
    {
        return DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc).ToString("o");
    }

    public static DateTime ParseUtcDateTime(this string dateText)
    {
        return DateTime.Parse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }
}

public static class Queries
{
    public static string InsertFile = @";alskdjf;";
    public static string InsertFiles = @"aslkdjf";
    public static string FileByPathHash = @"a;sldfj";
    public static string FIlesByExtensions = @"asdf";
    public static string FilesByParentDirectory = @";alksdjf";
    public static string Files = @"";
}

internal static class QueryFiles
{
    public static string CreateDatabase = @"shared.data.sql.CreateDatabase.sql";

    public static string TruncateDatabase = @"shared.data.sql.TruncateDatabase.sql";
}