using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared.TMDB;
using shared.TMDB.Models;
using shared;
using Xunit.Internal;
using System.Text.Json;
using Newtonsoft.Json;
using System.Data.Common;

namespace dj.test;

public class TMDB(ITestOutputHelper _output)
{
    private static IOptions<EndpointConfig> BasicOptions = Options.Create(new EndpointConfig
    {
        BaseUrl = "https://api.themoviedb.org/3",
        ApiKey = Repo.SUPER_SECRET_API_KEY,
        DatabasePath = "testdata/tmdb.db",
        RequestLimit = 40,
        RequestWindowSeconds = 10,
        TitleWeight = 100,
        OverviewWeight = 1
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

        var movies = await client.Query("Star Wars", 1);

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

        var queryResult = await repo.Query(searchTerm);

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, tokenSource.Token, -1, true); ;

        var queryHits = await repo.QueryTitle<MovieQueryResponse>(keywords, 1, tokenSource.Token);

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

        var queryResult = await repo.Query(searchTerm);

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, tokenSource.Token, -1, true); ;

        //query results
        var queryHits = await repo.QueryTitle<MovieQueryResponse>(keywords, 1, tokenSource.Token);

        queryHits.Should().NotBeNull();

        queryHits.Count().Should().BeGreaterThan(0);

        var resultDetails = queryHits.Where(x => x.Hits > 1).Select(x => x.Details);

        //get full movie details
        var movies = new List<MovieDetailsResponse?>();

        var resultMovies = resultDetails.SelectMany(x => x.results).DistinctBy(x => x.id).Where(x => x.adult == false);

        foreach (var result in resultMovies)
        {
            if (repo.TryMovie((long)result.id, out MovieDetailsResponse? movie))
            {
                movies.Add(movie);
            }
        }

        movies.Should().NotBeEmpty();

        // match movie titles
        var matchedMovies = new Dictionary<int, MatchScore<MovieDetailsResponse>>();

        foreach (var movie in movies)
        {
            if (movie is null) continue;

            var matchCount = SearchHelpers.MatchString(keywords, movie.title, tokenSource.Token) * BasicOptions.Value.TitleWeight;
            matchCount += SearchHelpers.MatchString(keywords, movie.overview, tokenSource.Token) * BasicOptions.Value.OverviewWeight;

            if (matchedMovies.ContainsKey(movie.id))
            {
                matchedMovies[movie.id].Hits += matchCount;
            }
            else
            {
                matchedMovies.Add(movie.id, new MatchScore<MovieDetailsResponse> { Hits = matchCount, Details = movie });
            }
        }

        int minimumHitCount = keywords.Count();
        Debug.WriteLine("----------------------------");
        matchedMovies.Where(x => x.Value.Hits >= minimumHitCount).ForEach(x => Debug.WriteLine($"{x.Value.Hits} - {x.Value.Details.title}"));
    }

    [Fact]
    public async Task GetTotalScoreForQuery()
    {
        CancellationTokenSource tokenSource = new();

        var minimumHitCount = 100;

        using Repo repo = new(BasicOptions, new Cache(BasicOptions), tokenSource);

        var searchTerm = "Training Day";

        var queryMatches = await repo.QueryMatches(searchTerm, minimumHitCount);

        Debug.WriteLine("----------------------------");
        queryMatches.Where(x => x.Hits >= minimumHitCount).ToList().ForEach(x => Debug.WriteLine($"{x.Hits} - {x.Details.id} - {x.Details.title} - {x.Details.vote_count} - {x.Details.budget} - {x.Details.overview.Substring(0, 20)}"));
    }
}
