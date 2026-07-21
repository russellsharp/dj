using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Diagnostics;
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

public interface IDatabase
{
    void Connect();
    void Create(CancellationToken? token = null);
    void Dispose(bool disposing);
    void Dispose();
    Task<File?> File(string path, CancellationToken? token = null);
    Task<bool> FileExists(string filePath, CancellationToken? token = null);
    Task<IEnumerable<File>> Files(CancellationToken? token = null);
    Task<IEnumerable<File>> Files(IEnumerable<string> paths, CancellationToken? token = null);
    Task<IEnumerable<File>> FilesByDirectory(IEnumerable<string> paths, CancellationToken? token = null);
    Task<IEnumerable<File>> FilesByExtensions(IEnumerable<string> extensions, CancellationToken? token = null);
    Task Insert(File file, CancellationToken? token = null);
    Task InsertOrUpdate(IEnumerable<File> testData, CancellationToken? token = null);
    Task Truncate(CancellationToken? token = null);
}

public class DatabaseNotConnected : Exception { }

public class Database : IDisposable, IDatabase
{
    private static readonly ConcurrentDictionary<string, object> s_databaseLocks = new();
    private readonly DatabaseConfiguration _config;
    private int _commandTimeoutSeconds = 20;
    private CancellationTokenSource _cts;

    private string DatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, _config.DataFile));
        }
    }

    private string ConnectionStringReadOnly
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            };
            return builder.ConnectionString;
        }
    }


    private string ConnectionStringReadWrite
    {
        get
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };
            return builder.ConnectionString;
        }
    }

    private SqliteConnection ConnectionRead
    {
        get
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? throw new InvalidOperationException("Unable to determine database directory"));

            var lockObject = s_databaseLocks.GetOrAdd(DatabasePath, _ => new object());
            lock (lockObject)
            {
                var connection = new SqliteConnection(ConnectionStringReadWrite);

                //uses its own connection with write permissions
                Create();

                connection.Open();

                return connection;
            }
        }
    }

    private SqliteConnection ConnectionWrite
    {
        get
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? throw new InvalidOperationException("Unable to determine database directory"));

            var lockObject = s_databaseLocks.GetOrAdd(DatabasePath, _ => new object());
            lock (lockObject)
            {
                var connection = new SqliteConnection(ConnectionStringReadWrite);

                //uses its own connection with write permissions
                Create();

                connection.Open();

                return connection;
            }
        }
    }

    public Database(IOptions<DatabaseConfiguration> config, CancellationTokenSource cts)
    {
        _config = config.Value;

        _cts = cts;

        SqlMapper.AddTypeHandler(new UtcDateTimeHandler());
    }

    /// <summary>
    /// Connects to the database file.  Will create the file and directory path if necessary.
    /// </summary>
    public void Connect()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? throw new InvalidOperationException("Unable to determine database directory"));

        var lockObject = s_databaseLocks.GetOrAdd(DatabasePath, _ => new object());
        lock (lockObject)
        {
            using var connection = new SqliteConnection(ConnectionStringReadWrite);

            //uses its own connection with write permissions
            Create();

            connection.Open();
        }
    }

    public void Create(CancellationToken? token = null)
    {
        Console.WriteLine("Creating");
        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        connection.Open();

        string query = GetQueryFromResource(QueryFiles.CreateDatabase);

        using (var transaction = connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction"))
        {
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
    }

    public async Task Insert(File file, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            using var connection = new SqliteConnection(ConnectionStringReadWrite);

            await connection.OpenAsync(token.Value);

            using var transaction = connection.BeginTransaction();
            const string sql = @"
            INSERT INTO file (
                path_hash, path, date_modified, date_created, 
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
                    date_modified = file.date_modified.ToIsoUtcString(),
                    date_created = file.date_created.ToIsoUtcString(),
                    file.size,
                    file.extension,
                    file.hash,
                    file.attributes,
                    file.extra_attributes
                };

                var command = new CommandDefinition(sql, parameters, transaction, _commandTimeoutSeconds, CommandType.Text, CommandFlags.Buffered, token.Value);
                await connection.ExecuteAsync(command);
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while inserting file record: {ex}");
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WHAT {ex}");
            throw;
        }
    }

    public async Task InsertOrUpdate(IEnumerable<File> testData, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            token.Value.ThrowIfCancellationRequested();

            using var connection = new SqliteConnection(ConnectionStringReadWrite);

            await connection.OpenAsync(token.Value);

            using var transaction = await connection.BeginTransactionAsync(token.Value);

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
                var command = new CommandDefinition(sql, batchedParameters, transaction, _commandTimeoutSeconds, CommandType.Text, CommandFlags.Buffered, token.Value);
                await connection.ExecuteAsync(command);

                await transaction.CommitAsync(token.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while inserting file record: {ex}");
                transaction.Rollback();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WHAT {ex}");
            throw;
        }
    }

    public async Task<bool> FileExists(string filePath, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            using (var connection = ConnectionRead)
            {
                await connection.OpenAsync(token.Value);
                const string sql = @"SELECT EXISTS (SELECT 1 FROM file WHERE path_hash = @path_hash)";
                var path_hash = FileHelper.HashString(Path.GetFullPath(filePath));
                var command = new CommandDefinition(sql, new { path_hash }, null, _commandTimeoutSeconds, CommandType.Text, CommandFlags.None, token.Value);
                var result = await connection.ExecuteScalarAsync<bool>(command);
                return result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while checking for file entry: {ex}");
            throw;
        }
    }

    public async Task<shared.data.File?> File(string path, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        await connection.OpenAsync(token.Value);

        const string sql = @"SELECT * FROM file WHERE path_hash = @path_hash;";

        shared.data.File? file = null;

        try
        {
            var path_hash = FileHelper.HashString(path);
            file = await connection.QueryFirstOrDefaultAsync<shared.data.File>(sql, new { path_hash = path_hash });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while querying file record: {ex}");
            throw;
        }

        return file;
    }

    public async Task<IEnumerable<shared.data.File>> Files(CancellationToken? token = null)
    {
        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        await connection.OpenAsync(token.Value);

        const string sql = @"SELECT * FROM file;";

        try
        {
            return await connection.QueryAsync<shared.data.File>(sql, null, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while querying file record: {ex}");
            throw;
        }
    }


    public async Task<IEnumerable<shared.data.File>> Files(IEnumerable<string> paths, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        await connection.OpenAsync(token.Value);

        const string sql = @"SELECT * FROM file WHERE path_hash IN @path_hashes;";

        var files = Enumerable.Empty<shared.data.File>();

        try
        {
            var path_hashes = paths.Select(x => FileHelper.HashString(x));
            files = await connection.QueryAsync<shared.data.File>(sql, new { path_hashes = path_hashes }, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while querying file record: {ex}");
            throw;
        }

        return files;
    }

    public async Task<IEnumerable<shared.data.File>> FilesByExtensions(IEnumerable<string> extensions, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        await connection.OpenAsync(token.Value);

        const string sql = @"SELECT * FROM file WHERE extension IN @file_extensions;";

        var files = Enumerable.Empty<shared.data.File>();

        try
        {
            files = await connection.QueryAsync<shared.data.File>(sql, new { file_extensions = extensions }, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while querying file record: {ex}");
            throw;
        }

        connection.Close();
        return files;
    }

    public async Task<IEnumerable<shared.data.File>> FilesByDirectory(IEnumerable<string> paths, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        await connection.OpenAsync(token.Value);

        var sql = new StringBuilder(@"SELECT * FROM file WHERE ");

        var conditions = new List<string>();
        var parameters = new DynamicParameters();
        var searchTerms = paths.Select(x => Path.GetDirectoryName(x) ?? string.Empty).ToList();
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
            files = await connection.QueryAsync<shared.data.File>(sql.ToString(), parameters, null, _commandTimeoutSeconds, CommandType.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while querying file record: {ex}");
            throw;
        }

        return files;
    }

    public async Task Truncate(CancellationToken? token = null)
    {

        token ??= _cts.Token;

        using var connection = new SqliteConnection(ConnectionStringReadWrite);

        await connection.OpenAsync(token.Value);

        string query = GetQueryFromResource(QueryFiles.TruncateDatabase);
        using (var transaction = connection?.BeginTransaction() ?? throw new NullReferenceException("Null database connection or failure to create transaction"))
        {
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