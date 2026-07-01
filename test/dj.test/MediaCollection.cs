using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared;
using shared.data;
using shared.TMDB;
using Xunit.Internal;

namespace dj.test;

public class MediaCollection
{
    private IOptions<MediaCollectionConfiguration> BasicMediaOptions = Options.Create(new MediaCollectionConfiguration
    {
        Filter = "*.*",
        // BaseDirectory = "C:/dev/mediaReference",
        BaseDirectory = @"//Fatty//existing",
        DirectoryRecursionDepth = 50,
        AudioExtensions = @"mp3",
        VideoExtensions = @"avi;mkv;mp4",
    });

    private IOptions<DatabaseConfiguration> BasicDatabaseConfig = Options.Create(new DatabaseConfiguration
    {
        DataFile = "testdata/mediacollection.db",
    });

    private static IOptions<EndpointConfig> BasicEndpointOptions = Options.Create(new EndpointConfig
    {
        BaseUrl = "https://api.themoviedb.org/3",
        ApiKey = Repo.SUPER_SECRET_API_KEY,
        DatabasePath = "testdata/tmdb.db",
        RequestLimit = 40,
        RequestWindowSeconds = 10,
        TitleWeight = 100,
        OverviewWeight = 1
    });

    private CancellationTokenSource _tokenSource = new();

    [Fact]
    public async Task MatchLocalFiles()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _tokenSource);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb);

        await media.Initialize(_tokenSource.Token);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _tokenSource.Token);

        var keywords = SearchHelpers.SanitizeForSearch("Trainingwah wahDay wee", _tokenSource.Token, false);


        var matches = (await media.FindInPath<shared.data.File>(keywords, null, _tokenSource.Token)).Select(x => x.Details);

        matches.Should().BeEmpty();

        matches.ForEach(x => Debug.WriteLine(x.path ?? string.Empty));

        keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _tokenSource.Token, true);

        var matcheScores = (await media.FindInPath<shared.data.File>(keywords, null, _tokenSource.Token)).ToList();

        matcheScores.Should().NotBeEmpty();

        Debug.WriteLine("Matches made:");
        matcheScores.ForEach(x => Debug.WriteLine(x.Details?.path ?? string.Empty));
    }

    [Fact]
    public async Task MatchLocalFilesDictionaryAction()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _tokenSource);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb);

        await media.Initialize(_tokenSource.Token);

        var keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _tokenSource.Token, false);

        var matcheScores = (await media.FindInPath<shared.data.File>(keywords, null, _tokenSource.Token)).ToList();

        matcheScores.Should().NotBeEmpty();

        matcheScores.Select(x => x.Details).ForEach(x => Debug.WriteLine(x?.path ?? string.Empty));
    }

    [Fact]
    public async Task MatchLocalFilesToTmdb()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _tokenSource);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _tokenSource.Token);

        await media.Initialize(_tokenSource.Token);

        var localMovies = await media.Files(MediaType.Video);

        localMovies.Should().NotBeNullOrEmpty();

        int a = 0;
        var movieTitle = a == 0 ? "Training Day" : "Inglourious Basterds";

        //sanitize the path to find simple titles
        var movieKeywords = SearchHelpers.SanitizeString(movieTitle);
        var localMovie = localMovies.FirstOrDefault(x => SearchHelpers.SanitizeString(x.path).Contains(movieKeywords));
        Debug.WriteLine($"'{movieKeywords}'");

        if (localMovie is null)
        {
            movieKeywords = SearchHelpers.SanitizeString(movieTitle);
            Debug.WriteLine(movieKeywords.ToString());
            localMovie = localMovies.FirstOrDefault(x => SearchHelpers.SanitizeString(x.path).Contains(movieKeywords));
        }

        localMovie.Should().NotBeNull();
        var matchedLocalMovie = localMovie ?? throw new InvalidOperationException("Expected a local movie match.");

        var context = new MatchingContext
        {
            MinimumScore = 100,
            PathDepthMin = 1,
            PathDepthMax = 2
        };

        var bestMatches = await tmdb.PathToTmdb(matchedLocalMovie.path ?? string.Empty, context, true, _tokenSource.Token);

        bestMatches.Should().NotBeNull();

        Debug.WriteLine($"Best matches found for: {matchedLocalMovie.path ?? string.Empty}");
        bestMatches.ForEach(x => Debug.WriteLine($"Best match found: {x?.id} - {x?.title ?? string.Empty}"));
    }

    [Fact]
    public async Task Match100LocalFilesToTmdb()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _tokenSource);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb);

        await media.Initialize(_tokenSource.Token);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _tokenSource.Token);

        var movieFiles = (await media.Files(MediaType.Video)).Take(100);

        foreach (var localMovie in movieFiles)
        {
            localMovie.Should().NotBeNull();

            var context = new MatchingContext
            {
                MinimumScore = 100,
                PathDepthMin = 1,
                PathDepthMax = 2
            };

            var bestMatches = await tmdb.PathToTmdb(localMovie.path ?? string.Empty, context, true, _tokenSource.Token);

            if (bestMatches is not null)
            {
                Debug.WriteLine($"Best matches for {localMovie.path ?? string.Empty}");
                bestMatches.ForEach(x => Debug.WriteLine($"{x?.id} - {x?.title ?? string.Empty}"));
            }
        }
    }
}