using System.Diagnostics;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using shared.TMDB.Models;

namespace shared.TMDB;


public class ClientConfig
{
    public required string BaseUrl { get; init; } = "https://api.themoviedb.org/3";

    public required string ApiKey { get; init; } = Client.SUPER_SECRET_API_KEY;
}

public class Client : IDisposable
{
    #region SUPER SECRET DO NOT LOOK
    public const string SUPER_SECRET_API_KEY = "eyJhbGciOiJIUzI1NiJ9.eyJhdWQiOiI5M2M2YjdiMzI2MzkzYWJlNDA3NjkyMzM2M2YxOWU1NyIsIm5iZiI6MTc4MTQ3ODA2MS40NzIsInN1YiI6IjZhMmYzMmFkZGIyYWI4YjZiOTRhYzgwOCIsInNjb3BlcyI6WyJhcGlfcmVhZCJdLCJ2ZXJzaW9uIjoxfQ.80AGun0FCgltxbKNzw7dHbhZFJlZL_NT105aaOiQHwk";
    #endregion SUPER SECRET IS OVER

    private const uint REQUEST_LIMIT = 40;

    private const uint REQUEST_WINDOW_SECONDS = 10;

    private ClientConfig _config;

    public void log(string msg)
    {
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
    }

    public Client(IOptions<ClientConfig> config)
    {
        _config = config.Value;
    }

    private string Language = "en-US";
    private bool IncludeAdult = true;

    public async Task<MovieQueryResponse> QueryMovie(string query, int page)
    {
        var client = new RestClient(_config.BaseUrl);
        var request = new RestRequest("search/movie", Method.Get);
        request.AddQueryParameter("query", query);
        request.AddQueryParameter("include_adult", IncludeAdult);
        request.AddQueryParameter("page", page);
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");
        var response = await client.GetAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return JsonConvert.DeserializeObject<MovieQueryResponse>(response.Content);
        }
        else
        {
            return new MovieQueryResponse();
        }
    }

    public async Task<MovieDetailsResponse> Movie(long id)
    {
        var client = new RestClient(_config.BaseUrl);
        var request = new RestRequest("movie/{movieId}");
        request.AddUrlSegment("movieId", id);
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        log(client.BuildUri(request).ToString());

        var response = await client.GetAsync(request);

        log(response.Content);
        return JsonConvert.DeserializeObject<MovieDetailsResponse>(response.Content);
    }

    public async Task<GenreResponse> MovieGenres()
    {
        var client = new RestClient(_config.BaseUrl);
        var request = new RestRequest("genre/movie/list");
        request.AddQueryParameter("language", Language);
        request.AddHeader("accept", "application/json");
        request.AddHeader("Authorization", $"Bearer {_config.ApiKey}");

        var response = await client.GetAsync(request);

        log(response.Content);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return JsonConvert.DeserializeObject<GenreResponse>(response.Content);
        }
        else
        {
            return new GenreResponse();
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