using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared.TMDB;
using shared.TMDB.Models;
using shared;
using Xunit.Internal;
using System.Text.Json;
using Newtonsoft.Json;

namespace dj.test;

public class TMDB(ITestOutputHelper _output)
{
    private static IOptions<EndpointConfig> BasicOptions = Options.Create(new EndpointConfig
    {
        BaseUrl = "https://api.themoviedb.org/3",
        ApiKey = Repo.SUPER_SECRET_API_KEY,
        DatabasePath = "testdata/tmdb.db",
        RequestLimit = 40,
        RequestWindowSeconds = 10
    });

    internal void log(string msg)
    {
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
        _output.WriteLine(msg);
    }

    [Fact]
    public async Task QueryMovies()
    {
        CancellationTokenSource tokenSource = new();
        using Repo client = new(BasicOptions, new Cache(BasicOptions), tokenSource);

        var movies = await client.QueryMovie("Star Wars", 1);

        movies.Should().NotBeNull();

        movies.results.Count().Should().BeGreaterThan(0);

        movies.results.ForEach(x => log(x.title));

        var firstMovie = movies.results[0];

        firstMovie.title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task QueryMovieGenres()
    {
        CancellationTokenSource tokenSource = new();
        using Repo client = new(BasicOptions, new Cache(BasicOptions), tokenSource);

        bool result = client.TryMovieGenres(out GenreResponse? genres);

        genres.Should().NotBeNull();

        genres.Genres.ForEach(x => log(x.Name));

        genres.Genres.Should().NotBeNullOrEmpty();

        genres.Genres.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MovieDetails()
    {
        CancellationTokenSource tokenSource = new();
        using Repo client = new(BasicOptions, new Cache(BasicOptions), tokenSource);
        var found = client.TryMovie(11, out MovieDetailsResponse details);

        found.Should().BeTrue();

        details.Should().NotBeNull();

        details.genres.Should().NotBeNull();

        details.genres.Count().Should().BeGreaterThan(0);

        details.id.Should().Be(11);

        details.title.Should().Be("Star Wars");
    }

    [Fact]
    public async Task GetScore()
    {
        CancellationTokenSource tokenSource = new();

        using Repo repo = new(BasicOptions, new Cache(BasicOptions), tokenSource);

        var searchTerm = "Star Wars".ToLower();

        var queryResult = await repo.QueryMovie(searchTerm);

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, true, tokenSource.Token); ;

        var queryHits = await repo.FindQueryHits<MovieQueryResponse>(keywords, 1, tokenSource.Token);

        queryHits.Should().NotBeNull();

        queryHits.Count().Should().BeGreaterThan(0);

        var resultDetails = queryHits.Where(x => x.Hits > 1).Select(x => x.Details as MovieQueryResponse);

        var movies = new List<MovieDetailsResponse>();

        var resultMovies = resultDetails.SelectMany(x => x.results).DistinctBy(x => x.id);

        foreach (var result in resultMovies)
        {
            if (repo.TryMovie((long)result.id, out MovieDetailsResponse movie))
            {
                movies.Add(movie);
            }
        }

        movies.Should().NotBeEmpty();

        movies.ToList().ForEach(x => Debug.WriteLine(x.title));
    }

    [Fact]
    public async Task GetScoreAndMatchRemoteMovies()
    {
        CancellationTokenSource tokenSource = new();

        using Repo repo = new(BasicOptions, new Cache(BasicOptions), tokenSource);

        var searchTerm = "Training Day";

        var queryResult = await repo.QueryMovie(searchTerm);

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, true, tokenSource.Token); ;

        //query results
        var queryHits = await repo.FindQueryHits<MovieQueryResponse>(keywords, 1, tokenSource.Token);

        queryHits.Should().NotBeNull();

        queryHits.Count().Should().BeGreaterThan(0);

        var resultDetails = queryHits.Where(x => x.Hits > 1).Select(x => x.Details as MovieQueryResponse);

        var movies = new List<MovieDetailsResponse>();

        var resultMovies = resultDetails.SelectMany(x => x.results).DistinctBy(x => x.id).Where(x => x.adult == false);

        foreach (var result in resultMovies)
        {
            if (repo.TryMovie((long)result.id, out MovieDetailsResponse movie))
            {
                movies.Add(movie);
            }
        }

        movies.Should().NotBeEmpty();

        var matchedMovies = movies.ToList().Where(x => keywords.Count() / SearchHelpers.MatchFileName(keywords, x.title, tokenSource.Token) > 0.45);

        Debug.WriteLine("----------------------------");

        matchedMovies.ForEach(x => Debug.WriteLine(x.title));
    }
}
