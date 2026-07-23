using System.Diagnostics;
using Microsoft.Extensions.Options;
using RestSharp;
using shared.TMDB.Models;
using shared.http;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;

namespace shared.TMDB;

public interface IRepo
{
    void Dispose();
    Task<MovieQueryResponse?> QueryTitle(string query, int page = 1, CancellationToken? token = null);
    Task<MovieDetailsResponse?> Movie(long id, CancellationToken? token = null);
    Task<GenreResponse?> MovieGenres(CancellationToken? token = null);
    Task<IEnumerable<MatchScore<ResponseType>>> QueryTitle<ResponseType>(IEnumerable<string> keywords, int mimimumHitCount, CancellationToken? token = null) where ResponseType : class;
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(string searchTerm, int minimumHitCount, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null);
}

public class Repo : IDisposable, IRepo
{
    private EndpointConfig _config;
    private ICache _cache;
    private readonly IRateLimiter _limiter;
    private readonly CancellationTokenSource _tokenSource;

    private void log(string msg)
    {
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
    }

    public Repo(IOptions<EndpointConfig> config, ICache cache, CancellationTokenSource tokenSource)
    {
        _config = config.Value;
        _cache = cache;
        _limiter = new RateLimiter(config);
        _tokenSource = tokenSource;
    }

    private string Language = "en-US";

    public async Task<string> DiscoverMovie(string query, int page = 1, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var request = new RestRequest("discover/movie?", Method.Get);
        request.AddQueryParameter("query", query);
        request.AddQueryParameter("include_adult", _config.IncludeAdult);
        request.AddQueryParameter("page", page);
        request.AddQueryParameter("language", Language);
        request.AddQueryParameter("include_video", false);
        request.AddQueryParameter("sort_by", "popularity.desc");
        request.AddQueryParameter("with_keywords", query);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        var response = await Get<MovieQueryResponse>(request, token);
        return response?.ToString() ?? string.Empty;
    }

    public async Task<MovieQueryResponse?> QueryTitle(string query, int page = 1, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var request = new RestRequest("search/movie", Method.Get);
        request.AddQueryParameter("query", query);
        request.AddQueryParameter("include_adult", _config.IncludeAdult);
        request.AddQueryParameter("page", page);
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        return await Get<MovieQueryResponse>(request, token);
    }

    public async Task<MovieDetailsResponse?> Movie(long id, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var request = new RestRequest("movie/{movieId}");
        request.AddUrlSegment("movieId", id);
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");
        return await Get<MovieDetailsResponse>(request, token);
    }

    public async Task<GenreResponse?> MovieGenres(CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var request = new RestRequest("genre/movie/list");
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        return await Get<GenreResponse>(request, token);
    }

    public async Task<IEnumerable<MatchScore<ResponseType>>> QueryTitle<ResponseType>(IEnumerable<string> keywords, int minimum_hits, CancellationToken? token = null) where ResponseType : class
    {
        token ??= _tokenSource.Token;

        return await _cache.FindQueryHits<ResponseType>(keywords, minimum_hits, token);
    }

    private async Task<ResponseType?> Get<ResponseType>(RestRequest request, CancellationToken? token = null) where ResponseType : new()
    {
        token ??= _tokenSource.Token;

        var requestUrl = _limiter.BuildUri(request);

        if (_cache.Get(requestUrl, out ResponseType? cachedResponse, token) && cachedResponse != null)
        {
            return cachedResponse;
        }
        else
        {
            var apiResponse = await _limiter.Get(request, token.Value);

            if (apiResponse.StatusCode == System.Net.HttpStatusCode.OK &&
                apiResponse.ResponseStatus != ResponseStatus.Error)
            {
                var content = apiResponse.Content ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(content))
                {
                    _cache.Store<ResponseType>(requestUrl, content, token).GetAwaiter().GetResult();
                    return JsonSerializer.Deserialize<ResponseType>(content) ?? new ResponseType();
                }

                return new ResponseType();
            }
            else
            {
                log($"Failed requesting from TMDB with response code: {apiResponse.StatusCode}");
                return default;
            }
        }
    }

    public async Task<List<MatchScore<MovieDetailsResponse>>> QueryMatches(string searchTerm, int minimumHitCount = 100, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, token.Value, true); ;

        //query results
        var queryHits = await QueryTitle<MovieQueryResponse>(keywords, minimumHitCount, token.Value);

        var resultDetails = queryHits.Select(x => x.Details).Where(x => x is not null).Cast<MovieQueryResponse>().ToList();

        //get full movie details
        var movies = new List<MovieDetailsResponse?>();

        var resultMovies = resultDetails
            .Where(x => x.results is not null)
            .SelectMany(x => x.results!)
            .Where(x => x is not null && x.id is not null)
            .DistinctBy(x => x!.id)
            .ToList();

        foreach (var x in resultMovies)
        {
            if (x?.id is not null)
            {
                movies.Add(await Movie(x.id.Value));
            }
        }

        // match movie titles
        var matchedMovies = new Dictionary<int, MatchScore<MovieDetailsResponse>>();

        foreach (var movie in movies)
        {
            if (movie is null) continue;

            var matchCount = SearchHelpers.MatchString(keywords, movie.title ?? string.Empty, token.Value) * _config.TitleWeight;
            matchCount += SearchHelpers.MatchString(keywords, movie.overview ?? string.Empty, token.Value) * _config.OverviewWeight;

            if (matchedMovies.ContainsKey(movie.id))
            {
                matchedMovies[movie.id].Hits += matchCount;
            }
            else
            {
                matchedMovies.Add(movie.id, new MatchScore<MovieDetailsResponse> { Hits = matchCount, Details = movie });
            }
        }

        return matchedMovies.Values.ToList();
    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(string searchTerm, int minimumHitCount, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, token.Value, true);

        var movieDetails = await _cache.QueryOverviews(keywords, minimumHitCount, token);

        return movieDetails.Where(x => x.Details is not null && x.Details.adult == _config.IncludeAdult);
    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        List<List<string>> groupedKeywords = new();

        foreach (var synonyms in query)
        {
            groupedKeywords.Add(synonyms.Select(x => SearchHelpers.SanitizeString(x)).ToList());
        }

        var movieDetails = await _cache.QueryWithGroupedTerms(groupedKeywords, minimumHitCount, token);

        return movieDetails.Where(x => x.Details is not null && x.Details.adult == _config.IncludeAdult);
    }

    #region IDisposable

    private int _disposed = 0;
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
        }
        catch (Exception)
        {
        }
    }
    #endregion IDisposable
}