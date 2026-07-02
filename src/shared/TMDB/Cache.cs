using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using shared.TMDB.Models;
using System.Net.Mime;
using System.Text.Json;
using System.Runtime.CompilerServices;
namespace shared.TMDB;

public interface ICache
{
    void Connect();
    void Disconnect();
    void Dispose();
    void EnsureConnected();
    bool IsConnected();
    void Create();
    Task Truncate();
    bool Get<ResponseType>(string tmdb_request_url, out ResponseType? response);
    Task Store<ResponseType>(string requestUrl, string? content);
    IAsyncEnumerable<ContentType?> GetAllStream<ContentType>(CancellationToken token);
    Task StoreTypedData<ResponseType>(ResponseType contents, CancellationToken? token = null);
    Task StoreMovieDetails(MovieDetailsResponse details, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<ResponseType>>> FindQueryHits<ResponseType>(IEnumerable<string> keywords, int minimum_hits, CancellationToken token) where ResponseType : class;
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(IEnumerable<string> keywords, int minimumHits, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> keywords, int minimumHits, CancellationToken? token = null);
}

public class Cache : IDisposable, ICache
{
    private EndpointConfig _config;
    private SqliteConnection? _connection = null;
    private static readonly ConcurrentDictionary<string, object> s_databaseLocks = new();

    private string DatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, _config.DatabasePath));
        }
    }

    public Cache(IOptions<EndpointConfig> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config.Value;

        Connect();

        Create();
    }

    private string ConnectionString
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

    public bool Get<ResponseType>(string tmdb_request_url, out ResponseType? response)
    {
        var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
        var sql = $"SELECT response FROM tmdb_cache WHERE url_hash = @request_hash AND response_type = @type";

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tmdb_request_url)));
        using var command = new SqliteCommand(sql, connection);
        try
        {
            command.Parameters.AddWithValue("request_hash", requestHash);
            command.Parameters.AddWithValue("type", typeof(ResponseType).ToString());

            var result = command.ExecuteScalar();

            if (result != null)
            {
                var json = Convert.ToString(result);
                response = !string.IsNullOrWhiteSpace(json)
                    ? JsonSerializer.Deserialize<ResponseType>(json)
                    : default;

                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while executing sql: {sql}, {ex}");
            response = default(ResponseType);
            throw;
        }
        response = default(ResponseType);
        return false;
    }

    public async Task Store<ResponseType>(string requestUrl, string? content)
    {
        var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
        var sql = "INSERT INTO tmdb_cache (url_hash, url, id, response, response_type) VALUES (@request_hash, @request, @response, @response_type) ON CONFLICT(url_hash) DO UPDATE SET response = excluded.response, response_type = excluded.response_type";

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestUrl)));
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("response", content);
        command.Parameters.AddWithValue("response_type", typeof(ResponseType).ToString());
        command.Parameters.AddWithValue("request", requestUrl);

        if (!string.IsNullOrWhiteSpace(content))
        {
            var typedContent = JsonSerializer.Deserialize<ResponseType>(content);
            if (typedContent is not null)
            {
                await StoreTypedData(typedContent);
            }
        }

        await command.ExecuteNonQueryAsync();
    }

    public async IAsyncEnumerable<ContentType?> GetAllStream<ContentType>([EnumeratorCancellation] CancellationToken token)
    {
        var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
        const string sql = "SELECT * FROM tmdb_cache";
        var dataSet = connection.QueryUnbufferedAsync<(string hash, string response)>(sql);

        await foreach (var row in dataSet.WithCancellation(token))
        {
            if (string.IsNullOrEmpty(row.response)) continue;

            yield return JsonSerializer.Deserialize<ContentType>(row.response);
        }
    }

    public async Task StoreTypedData<ResponseType>(ResponseType contents, CancellationToken? token = null)
    {
        switch (contents)
        {
            case MovieDetailsResponse details:
            {
                await StoreMovieDetails(details, token);
                break;
            }
            default:
            {
                throw new NotSupportedException($"{nameof(ResponseType)} is not supported for typed storage.");
            }
        }
    }

    public async Task StoreMovieDetails(MovieDetailsResponse details, CancellationToken? token = null)
    {
        var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
        const string sql = "INSERT INTO movie_details (id, details) VALUES (@id, @Details) ON CONFLICT(id) DO UPDATE SET details = EXCLUDED.details";
        var detailString = JsonSerializer.Serialize(details);
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("details", detailString);

        await command.ExecuteNonQueryAsync();
    }

    // search movie fields from tmdb for keywords and count hits for each movie
    public async Task<IEnumerable<MatchScore<ResponseType>>> FindQueryHits<ResponseType>(IEnumerable<string> keywords, int minimum_hits, CancellationToken token) where ResponseType : class
    {
        //         SELECT url_hash, response, 
        //                          (CASE WHEN description_field LIKE '%apple%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%banana%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%orange%' THEN 1 ELSE 0 END) AS hit_counts
        //         FROM products
        //         WHERE hit_count >= minimum_hits and response_type = ResponseType
        //         ORDER BY hit_counts;

        try
        {
            const string sqlPrefix = "SELECT response as Details, ";
            string suffix = $" AS Hits \n FROM tmdb_cache \n WHERE Hits >= {minimum_hits} AND response_type = '{typeof(ResponseType)}' \n ORDER BY Hits;";
            var caseStatements = keywords.Where(x => !string.IsNullOrEmpty(x)).Select(x => $"CASE WHEN response LIKE '%{x}%' THEN 1 ELSE 0 END");
            string sql = $"{sqlPrefix} ({string.Join(" + \n", caseStatements)}) {suffix}";

            var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
            var matches = await connection.QueryAsync(sql);
            var typedMatches = matches.Select(x =>
            {
                var json = x.Details as string;
                return new MatchScore<ResponseType>()
                {
                    Hits = x.Hits,
                    Details = !string.IsNullOrWhiteSpace(json) ? JsonSerializer.Deserialize<ResponseType>(json) : null
                };
            });
            return typedMatches;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while building hit list: {ex}");
            throw;
        }

    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(IEnumerable<string> keywords, int minimumHits, CancellationToken? token = null)
    {
        //         SELECT url_hash, response, 
        //                          (CASE WHEN description_field LIKE '%apple%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%banana%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%orange%' THEN 1 ELSE 0 END) AS hit_counts
        //         FROM tmdb_cache
        //         WHERE hit_count >= minimum_hits and response_type = ResponseType
        //         ORDER BY hit_counts;

        try
        {
            const string sqlPrefix = "SELECT response as details, ";
            string suffix = $" AS Hits \n FROM tmdb_cache \n WHERE response_type = '{typeof(MovieDetailsResponse)}' AND Hits >= {minimumHits} \n ORDER BY Hits;";
            var caseStatements = keywords.Where(x => !string.IsNullOrEmpty(x)).Select(x => $"CASE WHEN response LIKE '%{x}%' THEN 1 ELSE 0 END");
            string sql = $"{sqlPrefix} ({string.Join(" + \n", caseStatements)}) {suffix}";

            if (!caseStatements.Any()) return new List<MatchScore<MovieDetailsResponse>>();

            Debug.WriteLine($"Querying overview: {sql}");

            var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
            var matches = await connection.QueryAsync(sql);
            return matches.Select(x =>
            {
                var json = x.details as string;
                return new MatchScore<MovieDetailsResponse>()
                {
                    Hits = x.Hits,
                    Details = !string.IsNullOrWhiteSpace(json) ? JsonSerializer.Deserialize<MovieDetailsResponse>(json) : null
                };
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while building hit list: {ex}");
            throw;
        }
    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> keywordsWithSynonyms, int minimumHits, CancellationToken? token = null)
    {
        try
        {
            List<string> cases = new();
            foreach (var group in keywordsWithSynonyms)
            {
                var groupedLikes = string.Join(" OR ", group.Where(x => !string.IsNullOrEmpty(x)).Select(x => $"response LIKE '%{x}%'"));
                cases.Add(groupedLikes);
            }

            if (!cases.Any(x => !string.IsNullOrEmpty(x))) return new List<MatchScore<MovieDetailsResponse>>();

            const string sqlPrefix = "SELECT response as details, ";
            string suffix = $" AS Hits \n FROM tmdb_cache \n WHERE response_type = '{typeof(MovieDetailsResponse)}' AND Hits >= {minimumHits} \n ORDER BY Hits;";
            var caseStatements = cases.Where(x => !string.IsNullOrEmpty(x)).Select(x => $"(CASE WHEN {x} THEN 1 ELSE 0 END)");
            string sql = $"{sqlPrefix} ({string.Join(" + \n", caseStatements)}) {suffix}";

            Debug.WriteLine($"Querying overview with synonyms:\n {sql}");

            var connection = _connection ?? throw new InvalidOperationException("Cache is not connected.");
            var matches = await connection.QueryAsync(sql);
            return matches.Select(x =>
            {
                var json = x.details as string;
                return new MatchScore<MovieDetailsResponse>()
                {
                    Hits = x.Hits,
                    Details = !string.IsNullOrWhiteSpace(json) ? JsonSerializer.Deserialize<MovieDetailsResponse>(json) : null
                };
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while building hit list: {ex}");
            throw;
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

            Console.WriteLine(ConnectionString);
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
        var rootDir = Path.GetDirectoryName(Environment.ProcessPath);

#pragma warning disable CS8604 // Possible null reference argument.
        var dbPath = Path.GetFullPath(Path.Combine(rootDir, _config.DatabasePath));
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
        string query = GetQueryFromResource<shared.TMDB.Cache>(QueryFiles.CreateDatabase);

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

    public async Task Truncate()
    {
        string query = GetQueryFromResource<shared.TMDB.Cache>(QueryFiles.TruncateDatabase);
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

    private string GetQueryFromResource<ModuleName>(string resourceName)
    {
        //find embedded resources in shared library
        var assembly = typeof(ModuleName).Assembly;

        string? query = null;

        // assembly.GetManifestResourceNames().ToList().ForEach(x => Debug.WriteLine(x));

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

    internal static class QueryFiles
    {
        public static string CreateDatabase = @"shared.TMDB.sql.TMDB_Create.sql";

        public static string TruncateDatabase = @"shared.TMDB.sql.TMDB_Truncate.sql";
    }

    #region IDisposable
    private int _disposed = 0;


    public virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            if (disposing)
                if (_connection != null && _connection.State == ConnectionState.Open)
                    _connection?.Close();

            //dispose unmanaged objects
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Cache()
    {
        Dispose();
    }

    #endregion IDisposable

}
