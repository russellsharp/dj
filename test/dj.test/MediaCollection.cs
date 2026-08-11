using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using shared;
using shared.data;
using shared.TMDB;
using shared.utility;

namespace dj.test;

public class MediaCollection : BaseTest, IDisposable
{
    private static IOptions<MediaCollectionConfiguration> BasicMediaOptions = Options.Create(new MediaCollectionConfiguration
    {
        Filter = "*.*",
        // BaseDirectory = "testdata/mediaReference/",
        BaseDirectory = @"//Fatty//existing",
        DirectoryRecursionDepth = 50,
        AudioExtensions = @"mp3",
        VideoExtensions = @"avi;mkv;mp4",
    });

    private static IOptions<MediaDatabaseConfiguration> BasicDatabaseConfig = Options.Create(new MediaDatabaseConfiguration
    {
        DatabasePath = "testdata/mediacollection.db",
    });

    private static IOptions<TMDBConfiguration> BasicEndpointOptions = Options.Create(new TMDBConfiguration
    {
        BaseUrl = "https://api.themoviedb.org/3",
        DatabasePath = "testdata/tmdb.db",
        TitleWeight = 100,
        OverviewWeight = 1
    });

    private string MediaDatabasePath => Path.GetFullPath(BasicDatabaseConfig.Value.DatabasePath);

    private string TmdbDatabasePath => Path.GetFullPath(BasicEndpointOptions.Value.DatabasePath);

    public MediaCollection(ITestOutputHelper output) : base(output)
    {
        try
        {
            RestoreDatabase();
        }
        catch (Exception ex)
        {
            log($"Exception not caught in MediaCollection Tests ctor: {ex}");
        }
    }

    private static (IRepo repo, ITMDB tmdb, IMediaDatabase db, IMediaCollection medai) BuildServices(CancellationTokenSource cts)
    {
        IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions, new LoggerFactory().CreateLogger<ICache>(), cts), new LoggerFactory().CreateLogger<IRepo>(), cts);
        ITMDB tmdb = new shared.TMDB.TMDB(repo, cts);
        IMediaDatabase db = new shared.data.MediaDatabase(BasicDatabaseConfig, new LoggerFactory().CreateLogger<shared.data.MediaDatabase>(), cts);
        IMediaCollection media = new shared.MediaCollection(BasicMediaOptions, db, new LoggerFactory().CreateLogger<shared.MediaCollection>(), cts);

        return (repo, tmdb, db, media);
    }

    [Fact]
    public async Task MatchLocalFiles()
    {
        var (repo, tmdb, db, media) = BuildServices(_cts);

        await media.Initialize(_cts.Token);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _cts.Token);

        var keywords = SearchHelpers.SanitizeForSearch("Trainingwah wahDay wee", _cts.Token, false);

        var matches = (await media.FindInPath<shared.data.File>(keywords, null, _cts.Token)).Select(x => x.Details);

        matches.Should().BeEmpty();

        keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _cts.Token, true);

        var matcheScores = (await media.FindInPath<shared.data.File>(keywords, null, _cts.Token)).ToList();

        matcheScores.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MatchLocalFilesDictionaryAction()
    {
        var (repo, tmdb, db, media) = BuildServices(_cts);

        await media.Initialize(_cts.Token);

        var keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _cts.Token, false);

        var matcheScores = (await media.FindInPath<shared.data.File>(keywords, null, _cts.Token)).ToList();

        matcheScores.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MatchLocalFilesToTmdb()
    {
        var (repo, tmdb, db, media) = BuildServices(_cts);


        await media.Initialize(_cts.Token);

        await media.UpdateRepos(BasicMediaOptions.Value.BaseDirectory, false, _cts.Token);

        var localMovies = await media.Files(MediaType.Video);

        localMovies.Should().NotBeNullOrEmpty();

        var movieTitle = "Training Day";

        //sanitize the path to find simple titles
        var movieKeywords = SearchHelpers.SanitizeString(movieTitle);
        var localMovie = localMovies.FirstOrDefault(x => SearchHelpers.SanitizeString(x.path).Contains(movieKeywords));

        if (localMovie is null)
        {
            movieKeywords = SearchHelpers.SanitizeString(movieTitle);
            log(movieKeywords.ToString());
            localMovie = localMovies.FirstOrDefault(x => SearchHelpers.SanitizeString(x.path).Contains(movieKeywords));
        }

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

    [Fact]
    public async Task Match100LocalFilesToTmdb()
    {
        var (repo, tmdb, db, media) = BuildServices(_cts);

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

        var (repo, tmdb, db, media) = BuildServices(_cts);

        // Initialize first to ensure the DB is ready for updates/checks
        await media.Initialize(_cts.Token);

        // Act: Call UpdateRepos with a path that doesn't exist
        // We expect it to handle this gracefully without throwing an exception related to file system access.
        var updateTask = () => media.UpdateRepos(nonExistentPath, false, _cts.Token);

        // Assert: No exceptions should be thrown, and the internal state should remain consistent (or at least not crash).
        await updateTask.Should().NotThrowAsync();
    }

    [Fact]
    public async Task File_ThrowsExceptionForNonExistentDatabaseEntry()
    {
        // Arrange: Use a path that is guaranteed not to be in the database.
        var nonExistentPath = "C:/media/definitely/not/in/db.mp4";

        var (repo, tmdb, db, media) = BuildServices(_cts);

        await media.Initialize(_cts.Token);

        // Act & Assert: Expect a specific exception (e.g., KeyNotFoundException or custom DB exception)
        await Assert.ThrowsAsync<FileLoadException>(() => media.File(nonExistentPath));
    }

    [Fact]
    public async Task Search_ReturnsEmptyCollectionWhenNoMatches()
    {
        // Arrange: Use a non-matching pattern
        var nonExistentPattern = "non_existent_pattern";

        var (repo, tmdb, db, media) = BuildServices(_cts);

        await media.Initialize(_cts.Token);

        // Act: Call the method under test with a non-matching pattern
        var matches = await media.Search(new[] { nonExistentPattern }, _cts.Token);

        // Assert: Expect an empty list of results
        matches.Should().BeEmpty();
    }

    #region IDisposable
    private int _disposed = 0;

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                if (disposing)
                {
                    RestoreDatabase();
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private void RestoreDatabase()
    {
        var overwriteAttemptMax = 10;
        int attempt = 0;
        //Sqlite driver can be slow to release database file
        while (attempt < overwriteAttemptMax)
        {
            try
            {
                var directoryCreated = Directory.CreateDirectory(Path.GetDirectoryName(MediaDatabasePath)!);
                //we request GC so that SQLite.Data frees the database file.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                System.IO.File.Copy(ReferenceDatabasePath, MediaDatabasePath, true);
                System.IO.File.Copy(ReferenceTmdbDatabasePath, TmdbDatabasePath, true);
                break;
            }
            catch (Exception ex)
            {
                log($"Database failed to overwrite.  Will retry in 1 second.\r\n\t{MediaDatabasePath}\r\n\tDirectory path: {Path.GetDirectoryName(MediaDatabasePath)}\r\n\t{ex}");
                Task.Delay(TimeSpan.FromSeconds(1).Milliseconds);
                attempt++;
            }
        }
    }
    #endregion IDisposable
}