using System.Diagnostics;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using shared.TMDB.Models;
using shared.http;
using SQLitePCL;
using Microsoft.Data.Sqlite;

namespace shared.TMDB;

public interface IRepo
{
    void Dispose();
    Task<MovieQueryResponse?> QueryMovie(string query, int page = 1);
    bool TryMovie(long id, out MovieDetailsResponse? movie);
    MovieDetailsResponse? Movie(long id);
    bool TryMovieGenres(out GenreResponse? genre);
    GenreResponse? MovieGenres();
    Task<IEnumerable<MatchScore>> FindQueryHits<T>(IEnumerable<string> keywords, int v, CancellationToken token);
}

public class Repo : IDisposable, IRepo
{
    #region SUPER SECRET DO NOT LOOK
    public const string SUPER_SECRET_API_KEY = "eyJhbGciOiJIUzI1NiJ9.eyJhdWQiOiI5M2M2YjdiMzI2MzkzYWJlNDA3NjkyMzM2M2YxOWU1NyIsIm5iZiI6MTc4MTQ3ODA2MS40NzIsInN1YiI6IjZhMmYzMmFkZGIyYWI4YjZiOTRhYzgwOCIsInNjb3BlcyI6WyJhcGlfcmVhZCJdLCJ2ZXJzaW9uIjoxfQ.80AGun0FCgltxbKNzw7dHbhZFJlZL_NT105aaOiQHwk";
    #endregion SUPER SECRET IS OVER

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
    private bool IncludeAdult = true;

    public async Task<MovieQueryResponse?> QueryMovie(string query, int page = 1)
    {
        var request = new RestRequest("search/movie", Method.Get);
        request.AddQueryParameter("query", query);
        request.AddQueryParameter("include_adult", IncludeAdult);
        request.AddQueryParameter("page", page);
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        return Get<MovieQueryResponse>(request);
    }

    public bool TryMovie(long id, out MovieDetailsResponse? movie)
    {
        var request = new RestRequest("movie/{movieId}");
        request.AddUrlSegment("movieId", id);
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        movie = Get<MovieDetailsResponse>(request);

        return movie != null;
    }

    public MovieDetailsResponse? Movie(long id)
    {
        MovieDetailsResponse? movieDetails;
        TryMovie(id, out movieDetails);
        return movieDetails;
    }

    public bool TryMovieGenres(out GenreResponse? genre)
    {
        var request = new RestRequest("genre/movie/list");
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        genre = Get<GenreResponse>(request);

        return genre != null;
    }

    public GenreResponse? MovieGenres()
    {
        GenreResponse genres;
        TryMovieGenres(out genres);
        return genres;
    }

    public async Task<IEnumerable<MatchScore>> FindQueryHits<ResponseType>(IEnumerable<string> keywords, int minimum_hits, CancellationToken token)
    {
        return await _cache.FindQueryHits<ResponseType>(keywords, minimum_hits, token);
    }

    private ResponseType? Get<ResponseType>(RestRequest request) where ResponseType : new()
    {
        var requestUrl = _limiter.BuildUri(request);

        if (_cache.Get<ResponseType>(requestUrl, out ResponseType? cachedResponse) && cachedResponse != null)
        {
            return cachedResponse;
        }
        else
        {
            var apiResponse = _limiter.Get(request, _tokenSource.Token).GetAwaiter().GetResult();

            if (apiResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _cache.Store<ResponseType>(requestUrl, apiResponse.Content).GetAwaiter().GetResult();

                return JsonConvert.DeserializeObject<ResponseType>(apiResponse.Content!);
            }
            else
            {
                Debug.WriteLine($"Failed requesting from TMDB with response code: {apiResponse.StatusCode}");
                return new ResponseType();
            }
        }
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
        catch (Exception ex)
        {
        }
    }
    #endregion IDisposable
}