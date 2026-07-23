using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared.TMDB;
using shared.TMDB.Models;
using shared;
using Xunit.Internal;
using System.Text.Json;
using System.Data.Common;
using shared.thesaurus;
using Microsoft.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace dj.test;

public class TMDB : BaseTest
{
    private static IOptions<TMDBConfiguration> BasicOptions = Options.Create(new TMDBConfiguration
    {
        BaseUrl = "https://api.themoviedb.org/3",
        DatabasePath = "testdata/tmdb.db",
        RequestLimit = 40,
        RequestWindowSeconds = 10,
        TitleWeight = 100,
        OverviewWeight = 1
    });

    private static IOptions<ThesaurusConfiguration> thesaurusOptionsDefaults = Options.Create(new ThesaurusConfiguration()
    {
        DictionaryPath = "wordnet/staticdata/",
        DatabasePath = "wordnet/database/wordnet.db"
    });

    private string TmdbDatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, BasicOptions.Value.DatabasePath));
        }
    }

    public TMDB(ITestOutputHelper output) : base(output)
    {
        try
        {
            DeleteDatabase();

            BasicOptions.Value.ApiKey = TMDBConfiguration.GetApiKey();

            log($"API KEY IS GOT: {!string.IsNullOrEmpty(BasicOptions.Value.ApiKey)}");
        }
        catch (Exception ex)
        {
            log($"Exception during database clear:\r\n{ex}");
        }
    }

    [Fact]
    public async Task QueryMovies()
    {
        using Repo client = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);

        var movies = await client.QueryTitle("Star Wars", 1);

        movies.Should().NotBeNull();

        movies.results.Count().Should().BeGreaterThan(0);

        var firstMovie = movies.results[0];

        firstMovie.title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task QueryMovieGenres()
    {
        using Repo client = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);

        var genres = await client.MovieGenres();

        genres.Should().NotBeNull();

        genres.Genres.Should().NotBeNullOrEmpty();

        genres.Genres.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MovieDetails()
    {
        using Repo client = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);
        var details = await client.Movie(11);

        details.Should().NotBeNull();

        details.genres.Should().NotBeNull();

        details.genres.Count().Should().BeGreaterThan(0);

        details.id.Should().Be(11);

        details.title.Should().Be("Star Wars");
    }

    [Fact]
    public async Task StoreMovieDetails()
    {
        const int StarWarsId = 11;

        using Repo client = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);
        var details = await client.Movie(11);

        details.Should().NotBeNull();

        details.genres.Should().NotBeNull();

        details.genres.Count().Should().BeGreaterThan(0);

        details.id.Should().Be(StarWarsId);

        details.title.Should().Be("Star Wars");
    }


    [Fact]
    public async Task GetScore()
    {
        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);

        var searchTerm = "Star Wars".ToLower();

        var queryResult = await repo.QueryTitle(searchTerm);

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, base._cts.Token, true); ;

        var queryHits = await repo.QueryTitle<MovieQueryResponse>(keywords, keywords.Count(), base._cts.Token);

        queryHits.Should().NotBeNull();

        queryHits.Count().Should().BeGreaterThan(0);

        var resultDetails = queryHits.Where(x => x.Hits > 1).Select(x => x.Details as MovieQueryResponse);

        var movies = new List<MovieDetailsResponse>();

        var resultMovies = resultDetails.SelectMany(x => x.results).DistinctBy(x => x.id);

        foreach (var result in resultMovies)
        {
            movies.Add(await repo.Movie((long)result.id));
        }

        movies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetScoreAndMatchRemoteMovies()
    {
        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), base._cts);

        var searchTerm = "Training Day";

        var queryResult = await repo.QueryTitle(searchTerm);

        var keywords = SearchHelpers.SanitizeForSearch(searchTerm, base._cts.Token, true); ;

        //query results
        var queryHits = await repo.QueryTitle<MovieQueryResponse>(keywords, 1, base._cts.Token);

        queryHits.Should().NotBeNull();

        queryHits.Count().Should().BeGreaterThan(0);

        var resultDetails = queryHits.Where(x => x.Hits > 1).Select(x => x.Details);

        //get full movie details
        var movies = new List<MovieDetailsResponse?>();

        var resultMovies = resultDetails.SelectMany(x => x.results).DistinctBy(x => x.id).Where(x => x.adult == false);

        foreach (var result in resultMovies)
        {
            movies.Add(await repo.Movie((long)result.id));
        }

        movies.Should().NotBeEmpty();

        // match movie titles
        var matchedMovies = new Dictionary<int, MatchScore<MovieDetailsResponse>>();

        foreach (var movie in movies)
        {
            if (movie is null) continue;

            var matchCount = SearchHelpers.MatchString(keywords, movie.title, _cts.Token) * BasicOptions.Value.TitleWeight;
            matchCount += SearchHelpers.MatchString(keywords, movie.overview, _cts.Token) * BasicOptions.Value.OverviewWeight;

            if (matchedMovies.ContainsKey(movie.id))
            {
                matchedMovies[movie.id].Hits += matchCount;
            }
            else
            {
                matchedMovies.Add(movie.id, new MatchScore<MovieDetailsResponse> { Hits = matchCount, Details = movie });
            }
        }
    }

    [Fact]
    public async Task GetTotalScoreForQuery()
    {
        var minimumHitCount = 100;

        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);

        var searchTerm = "Training Day";

        var queryMatches = await repo.QueryMatches(searchTerm, minimumHitCount);
    }

    [Fact(Skip = "Endpoint is broken.")]
    public async Task DiscoverMovie()
    {
        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);

        var searchTerm = "First|day";

        var response = await repo.DiscoverMovie(searchTerm);
    }

    [Fact]
    public async Task MatchByOverview()
    {
        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);

        var movies = await tmdb.QueryTitle("Training Day");

        // movie details will be stored in database
        foreach (var movie in movies.results)
        {
            _ = await tmdb.GetMovie(movie.id.Value);
        }

        //search movie details in database by matching terms in their overview
        var searchTerm = "First day".ToLower();

        var minimumHitCount = searchTerm.Split(' ').Count();

        var queryMatches = await tmdb.QueryOverviews(searchTerm, minimumHitCount);

        queryMatches.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MatchKeywordsByAll()
    {
        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);
        using ITMDB tmdb = new shared.TMDB.TMDB(repo, base._cts);

        var movies = await tmdb.QueryTitle("Training Day");

        // movie details will be stored in database

        if (movies?.results is not null)
        {
            foreach (var movie in movies.results)
            {
                if (movie.id is not null)
                {
                    _ = await tmdb.GetMovie(movie.id.Value);
                }
            }
        }

        //search movie details in database by matching terms in their overview
        var searchTerm = "police drama".ToLower();

        var minimumHitCount = searchTerm.Split(' ').Count();

        var queryMatches = await tmdb.QueryOverviews(searchTerm, minimumHitCount);

        var thesus = new Thesaurus(thesaurusOptionsDefaults);

        var searchTerms = searchTerm.Split(' ').ToList();

        var synonymTasks = searchTerms.Select(async x => await thesus.Search(x));

        var synonyms = await Task.WhenAll(synonymTasks);

        minimumHitCount = (int)(synonyms.Count() * 0.50);

        queryMatches = await tmdb.QueryWithGroupedTerms(synonyms.ToList(), minimumHitCount);

        queryMatches.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MatchKeywordsCollection()
    {
        using Repo repo = new(BasicOptions, new Cache(BasicOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);

        var movies = await tmdb.QueryTitle("Inglourious Basterds");

        // movie details will be stored in database
        foreach (var movie in movies.results)
        {
            _ = await tmdb.GetMovie(movie.id.Value);
        }

        //search movie details in database by matching terms in their overview
        var searchTerm = "world war 2".ToLower();

        var minimumHitCount = searchTerm.Split(' ').Count();

        List<MatchScore<MovieDetailsResponse>> queryMatches = (await tmdb.QueryOverviews(searchTerm, minimumHitCount)).ToList();

        var thesus = new Thesaurus(thesaurusOptionsDefaults);

        var searchTerms = searchTerm.Split(' ').ToList();

        var synonymTasks = searchTerms.Select(async x => (await thesus.Search(x)).ToList());

        List<List<string>> synonyms = (await Task.WhenAll(synonymTasks)).ToList();

        //add original terms as a group
        synonyms.Add(searchTerm.Split(' ').ToList());

        minimumHitCount = (int)(synonyms.Count() * 0.50);

        queryMatches.AddRange(await tmdb.QueryWithGroupedTerms(synonyms.ToList(), minimumHitCount));

        queryMatches.Should().NotBeNullOrEmpty();
    }

    #region IDisposable
    private int _disposed = 0;

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            if (disposing)
            {
                base.Dispose(disposing);
                DeleteDatabase();
            }
        }
    }

    private void DeleteDatabase()
    {
        var deleteAttemptsMax = 10;
        int attempt = 0;
        //Sqlite driver can be slow to release database file

        if (!File.Exists(TmdbDatabasePath)) return;

        while (attempt < deleteAttemptsMax)
        {
            try
            {
                //we request GC so that SQLite.Data frees the database file.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.IO.File.Delete(TmdbDatabasePath);
                break;
            }
            catch (Exception ex)
            {
                log($"Failed to delete: {TmdbDatabasePath}\r\nDirectory path: {Path.GetDirectoryName(TmdbDatabasePath)}\r\n{ex}");
                Task.Delay(TimeSpan.FromSeconds(1).Milliseconds);
                attempt++;
            }
        }

        if (attempt >= deleteAttemptsMax)
        {
            throw new InvalidOperationException("Failed to delete TMDB database.");
        }
    }
    #endregion IDisposable
}
