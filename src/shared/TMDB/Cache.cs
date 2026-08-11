using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using shared.data;
using shared.TMDB.Models;
namespace shared.TMDB;

public interface ICache
{
    bool Get<ResponseType>(string tmdb_request_url, out ResponseType? response, CancellationToken? token = null);
    Task Store<ResponseType>(string requestUrl, string? content, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<ResponseType>>> FindQueryHits<ResponseType>(IEnumerable<string> keywords, uint minimumHits, CancellationToken? token = null) where ResponseType : class;
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(IEnumerable<string> keywords, uint minimumHits, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> keywords, uint minimumHits, CancellationToken? token = null);
    void Dispose();
}

public class Cache : BaseSqliteDatabase, ICache
{
    private readonly CancellationTokenSource _tokenSource;
    private readonly ILogger<ICache> _logger;
    protected override string CreateQueryResource => QueryFiles.CreateDatabase;
    protected override string TruncateQueryResource => QueryFiles.TruncateDatabase;
    protected override Type QueryAssemblyType => typeof(Cache);

    public Cache(IOptions<TMDBConfiguration> config, ILogger<ICache> logger, CancellationTokenSource cts)
    {
        _logger = logger;

        ArgumentNullException.ThrowIfNull(config);

        _config = config.Value;

        _tokenSource = cts;
    }

    public bool Get<ResponseType>(string tmdb_request_url, out ResponseType? response, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        EnsureConnected();

        lock (GetLock())
        {
            var sql = $"SELECT response FROM tmdb_cache WHERE url_hash = @request_hash AND response_type = @type";

            var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tmdb_request_url)));

            try
            {
                var parameters = new { request_hash = requestHash, type = typeof(ResponseType).ToString() };
                var command = new CommandDefinition(sql, parameters, null, _commandTimeoutMs, CommandType.Text, CommandFlags.None, token.Value);
                var result = _connection.ExecuteScalar<string>(command);

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
                _logger.LogDebug($"Error while executing sql: {sql}, {ex}");
                response = default;
                throw;
            }
            finally
            {
                _connection.Dispose();
            }
            response = default;
        }
        return false;
    }

    public async Task Store<ResponseType>(string requestUrl, string? content, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        EnsureConnected();

        lock (GetLock())
        {

            var sql = "INSERT INTO tmdb_cache (url_hash, url, response, response_type) VALUES (@request_hash, @request, @response, @response_type) ON CONFLICT(url_hash) DO UPDATE SET response = excluded.response, response_type = excluded.response_type";

            var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestUrl)));
            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("request_hash", requestHash);
            command.Parameters.AddWithValue("response", content);
            command.Parameters.AddWithValue("response_type", typeof(ResponseType).ToString());
            command.Parameters.AddWithValue("request", requestUrl);

            //TODO: Implement typed data store if useful
            // await StoreTypedData(JsonSerializer.Deserialize<ResponseType>(content));

            command.ExecuteNonQuery();
        }
    }

    // search movie fields from tmdb for keywords and count hits for each movie
    public async Task<IEnumerable<MatchScore<ResponseType>>> FindQueryHits<ResponseType>(IEnumerable<string> keywords, uint minimumHits, CancellationToken? token = null) where ResponseType : class
    {
        //         SELECT url_hash, response, 
        //                          (CASE WHEN description_field LIKE '%apple%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%banana%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%orange%' THEN 1 ELSE 0 END) AS hit_counts
        //         FROM products
        //         WHERE hit_count >= minimum_hits and response_type = ResponseType
        //         ORDER BY hit_counts;

        token ??= _tokenSource.Token;

        EnsureConnected();

        lock (GetLock())
        {
            try
            {
                const string sqlPrefix = "SELECT response as Details, ";
                string suffix = $" AS Hits \n FROM tmdb_cache \n WHERE Hits >= {minimumHits} AND response_type = '{typeof(ResponseType)}' \n ORDER BY Hits;";
                var caseStatements = keywords.Where(x => !string.IsNullOrEmpty(x)).Select(x => $"CASE WHEN response LIKE '%{x}%' THEN 1 ELSE 0 END");
                string sql = $"{sqlPrefix} ({string.Join(" + \n", caseStatements)}) {suffix}";

                var matches = _connection.Query(sql);
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
                _logger.LogDebug($"Error while building hit list: {ex}");
                throw;
            }
        }
    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(IEnumerable<string> keywords, uint minimumHits, CancellationToken? token = null)
    {
        //         SELECT url_hash, response, 
        //                          (CASE WHEN description_field LIKE '%apple%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%banana%' THEN 1 ELSE 0 END +
        //                          CASE WHEN description_field LIKE '%orange%' THEN 1 ELSE 0 END) AS hit_counts
        //         FROM tmdb_cache
        //         WHERE hit_count >= minimum_hits and response_type = ResponseType
        //         ORDER BY hit_counts;

        token ??= _tokenSource.Token;

        EnsureConnected();

        lock (GetLock())
        {
            try
            {
                const string sqlPrefix = "SELECT response as details, ";
                string suffix = $" AS Hits \n FROM tmdb_cache \n WHERE response_type = '{typeof(MovieDetailsResponse)}' AND Hits >= {minimumHits} \n ORDER BY Hits;";
                var caseStatements = keywords.Where(x => !string.IsNullOrEmpty(x)).Select(x => $"CASE WHEN response LIKE '%{x}%' THEN 1 ELSE 0 END");
                string sql = $"{sqlPrefix} ({string.Join(" + \n", caseStatements)}) {suffix}";

                if (!caseStatements.Any()) return new List<MatchScore<MovieDetailsResponse>>();

                var matches = _connection.Query(sql);
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
                _logger.LogDebug($"Error while building hit list: {ex}");
                throw;
            }
        }
    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> keywordsWithSynonyms, uint minimumHits, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        EnsureConnected();

        lock (GetLock())
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

                var matches = _connection.Query(sql);
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
                _logger.LogError($"Error while building hit list: {ex}");
                throw;
            }
        }
    }

    public Task StoreTypedData<ResponseType>(ResponseType contents, CancellationToken? token = null) => contents switch
    {
        MovieDetailsResponse details => StoreMovieDetails(details, token),
        MovieQueryResponse result => StoreMovieQuery(result, token),
        _ => throw new NotSupportedException($"{typeof(ResponseType).Name} is not supported for typed storage.")
    };

    public async Task StoreMovieDetails(MovieDetailsResponse details, CancellationToken? token = null)
    {

        throw new NotImplementedException();


        EnsureConnected();

        lock (GetLock())
        {
            const string sql = "INSERT INTO movie_details (id, details, title, overview) VALUES (@id, @Details, @title, @overview) ON CONFLICT(id) DO UPDATE SET details = EXCLUDED.details";
            var detailString = JsonSerializer.Serialize(details);
            using var command = new SqliteCommand(sql, _connection);
            command.Parameters.AddWithValue("id", details.id);
            command.Parameters.AddWithValue("details", detailString);
            command.Parameters.AddWithValue("overview", details.overview);
            command.Parameters.AddWithValue("title", details.title);

            command.ExecuteNonQuery();
        }
    }

    public async Task StoreMovieQuery(MovieQueryResponse result, CancellationToken? token = null)
    {
        EnsureConnected();

        lock (GetLock())
        {
            throw new NotImplementedException();
        }
    }

    internal static class QueryFiles
    {
        public static string CreateDatabase = @"shared.TMDB.sql.TMDB_Create.sql";

        public static string TruncateDatabase = @"shared.TMDB.sql.TMDB_Truncate.sql";
    }
}
