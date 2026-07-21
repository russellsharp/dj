using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using shared;
using shared.data;
using shared.TMDB;
using Xunit.Internal;

namespace dj.test;

public class MediaCollection : BaseTest, IDisposable
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

    private string DatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, BasicDatabaseConfig.Value.DataFile));
        }
    }

    public MediaCollection(ITestOutputHelper output) : base(output)
    {
        try
        {
            RestoreDatabase();
        }
        catch (Exception ex)
        {
            log(ex.ToString());
        }
    }

    [Fact]
    public async Task MatchLocalFiles()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        await media.Initialize(_cts.Token);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _cts.Token);

        var keywords = SearchHelpers.SanitizeForSearch("Trainingwah wahDay wee", _cts.Token, false);

        var matches = (await media.FindInPath<shared.data.File>(keywords, null, _cts.Token)).Select(x => x.Details);

        matches.Should().BeEmpty();

        keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _cts.Token, true);

        log($"BaseDirectory: {BasicMediaOptions.Value.BaseDirectory}");
        log("Keywords:");
        keywords.ForEach(x => log(x));

        var matcheScores = (await media.FindInPath<shared.data.File>(keywords, null, _cts.Token)).ToList();

        matcheScores.Should().NotBeEmpty();

        log("Matches made:");
        matcheScores.ForEach(x => log(x.Details?.path ?? string.Empty));
    }

    [Fact]
    public async Task MatchLocalFilesDictionaryAction()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        await media.Initialize(_cts.Token);

        var keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _cts.Token, false);

        var matcheScores = (await media.FindInPath<shared.data.File>(keywords, null, _cts.Token)).ToList();

        matcheScores.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MatchLocalFilesToTmdb()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _cts.Token);

        await media.Initialize(_cts.Token);

        var localMovies = await media.Files(MediaType.Video);

        localMovies.Should().NotBeNullOrEmpty();

        var movieTitle = "Training Day";

        //sanitize the path to find simple titles
        var movieKeywords = SearchHelpers.SanitizeString(movieTitle);
        var localMovie = localMovies.FirstOrDefault(x => SearchHelpers.SanitizeString(x.path).Contains(movieKeywords));
        log($"'{movieKeywords}'");

        if (localMovie is null)
        {
            movieKeywords = SearchHelpers.SanitizeString(movieTitle);
            log(movieKeywords.ToString());
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

        var bestMatches = await tmdb.PathToTmdb(matchedLocalMovie.path ?? string.Empty, context, true, _cts.Token);

        bestMatches.Should().NotBeNull();
    }

    [Fact]
    public async Task Match100LocalFilesToTmdb()
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        await media.Initialize(_cts.Token);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _cts.Token);

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

            var bestMatches = await tmdb.PathToTmdb(localMovie.path ?? string.Empty, context, true, _cts.Token);

            bestMatches.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UpdateRepos_HandlesNonExistentDirectory()
    {
        // Arrange: Use a base directory that is guaranteed not to exist
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        // Initialize first to ensure the DB is ready for updates/checks
        await media.Initialize(_cts.Token);

        // Act: Call UpdateRepos with a path that doesn't exist
        // We expect it to handle this gracefully without throwing an exception related to file system access.
        await media.UpdateRepos(nonExistentPath, false, _cts.Token);

        // Assert: No exceptions should be thrown, and the internal state should remain consistent (or at least not crash).
    }

    [Fact]
    public async Task File_ThrowsExceptionForNonExistentDatabaseEntry()
    {
        // Arrange: Use a path that is guaranteed not to be in the database.
        var nonExistentPath = "C:/media/definitely/not/in/db.mp4";

        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        await media.Initialize(_cts.Token);

        // Act & Assert: Expect a specific exception (e.g., KeyNotFoundException or custom DB exception)
        await Assert.ThrowsAsync<FileLoadException>(() => media.File(nonExistentPath));
    }

    [Fact]
    public async Task Search_ReturnsEmptyCollectionWhenNoMatches()
    {
        // Arrange: Use a non-matching pattern
        var nonExistentPattern = "non_existent_pattern";

        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, _cts), _cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, _cts);
        IDatabase db = new shared.data.Database(BasicDatabaseConfig, _cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, tmdb, _cts);

        await media.Initialize(_cts.Token);

        // Act: Call the method under test with a non-matching pattern
        var matches = await media.Search(new[] { nonExistentPattern }, _cts.Token);

        // Assert: Expect an empty list of results
        matches.Should().BeEmpty();
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
                RestoreDatabase();
            }
        }
    }

    private void RestoreDatabase()
    {
        var deletionTryMax = 10;
        int tries = 0;
        //Sqlite driver can be slow to release database file
        while (tries < deletionTryMax)
        {
            try
            {
                //we request GC so that SQLite.Data frees the database file.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.IO.File.Copy(ReferenceDatabasePath, DatabasePath, true);
                break;
            }
            catch
            {
                Task.Delay(TimeSpan.FromSeconds(1).Milliseconds);
                tries++;
            }
        }
    }
    #endregion IDisposable
}