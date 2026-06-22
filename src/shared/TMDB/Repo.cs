using System.Diagnostics;
using Microsoft.Extensions.Options;
using RestSharp;
using shared.TMDB.Models;
using shared.http;
using SQLitePCL;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;

namespace shared.TMDB;

public interface IRepo
{
    void Dispose();
    Task<MovieQueryResponse?> QueryTitle(string query, int page = 1);
    bool TryMovie(long id, out MovieDetailsResponse? movie);
    MovieDetailsResponse? Movie(long id);
    bool TryMovieGenres(out GenreResponse? genre);
    GenreResponse? MovieGenres();
    Task<IEnumerable<MatchScore<ResponseType>>> QueryTitle<ResponseType>(IEnumerable<string> keywords, int mimimumHitCount, CancellationToken token) where ResponseType : class;
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(string searchTerm, int minimumHitCount, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviewsWithSynonyms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null);
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

    //var options = new RestClientOptions("https://api.themoviedb.org/3/discover/movie?
    // include_adult=false&
    // include_video=false&
    // language=en-US&
    // page=1&
    // sort_by=popularity.desc&
    // with_keywords=ant-man");

    public async Task<string> DiscoverMovie(string query, int page = 1, CancellationToken? token = null)
    {
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

        return Get<MovieQueryResponse>(request).ToString();
    }

    public async Task<MovieQueryResponse?> QueryTitle(string query, int page = 1)
    {
        var request = new RestRequest("search/movie", Method.Get);
        request.AddQueryParameter("query", query);
        request.AddQueryParameter("include_adult", _config.IncludeAdult);
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

    public GenreResponse MovieGenres()
    {
        TryMovieGenres(out GenreResponse genres);
        return genres;
    }

    public async Task<IEnumerable<MatchScore<ResponseType>>> QueryTitle<ResponseType>(IEnumerable<string> keywords, int minimum_hits, CancellationToken token) where ResponseType : class
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

            Debug.WriteLine(apiResponse.Content);

            if (apiResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _cache.Store<ResponseType>(requestUrl, apiResponse.Content).GetAwaiter().GetResult();

                return JsonSerializer.Deserialize<ResponseType>(apiResponse.Content!);
            }
            else
            {
                Debug.WriteLine($"Failed requesting from TMDB with response code: {apiResponse.StatusCode}");
                return new ResponseType();
            }
        }
    }

    public async Task<List<MatchScore<MovieDetailsResponse>>> QueryMatches(string searchTerm, int minimumHitCount = 100, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, token.Value, true); ;

        //query results
        var queryHits = await QueryTitle<MovieQueryResponse>(keywords, minimumHitCount, token.Value);

        var resultDetails = queryHits.Select(x => x.Details);

        //get full movie details
        var movies = new List<MovieDetailsResponse?>();

        var resultMovies = resultDetails.SelectMany(x => x.results).DistinctBy(x => x.id); //.Where(x => x.adult == _config.IncludeAdult);

        resultMovies.ToList().ForEach(x => movies.Add(Movie((long)x.id)));

        // match movie titles
        var matchedMovies = new Dictionary<int, MatchScore<MovieDetailsResponse>>();

        foreach (var movie in movies)
        {
            if (movie is null) continue;

            var matchCount = SearchHelpers.MatchString(keywords, movie.title, token.Value) * _config.TitleWeight;
            matchCount += SearchHelpers.MatchString(keywords, movie.overview, token.Value) * _config.OverviewWeight;

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

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, token.Value, true); ;

        var movieDetails = await _cache.QueryOverviews(keywords, minimumHitCount, token);

        return movieDetails.Where(x => x.Details.adult == _config.IncludeAdult);
    }

    public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviewsWithSynonyms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

        List<List<string>> groupedKeywords = new();

        foreach (var synonyms in query)
        {
            groupedKeywords.Add(synonyms.Select(x => SearchHelpers.SanitizeString(x)).ToList());
        }

        List<MatchScore<MovieDetailsResponse>> movieDetails = (await _cache.QueryOverviewsWithSynonyms(groupedKeywords, minimumHitCount, token)).ToList();

        return movieDetails.Where(x => x.Details.adult == _config.IncludeAdult);
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