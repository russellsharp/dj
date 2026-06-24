using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using System.Net.Mail;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared.data;
using Newtonsoft.Json;
using Xunit.Internal;
using shared.thesaurus;

namespace dj.test;

[CollectionDefinition("Application Level Tests", DisableParallelization = true)]
public class DatabaseCollectionDefinition { }

// Application level tests are run sequentially because they share a database
[Collection("Application Level Tests")]
public class ApplicationServices : IDisposable
{

    private HostApplicationBuilder _builder;
    private IHost _host;
    private ITestOutputHelper _output;
    private List<string> _filesToDelete = new();

    private CancellationTokenSource _tokenSource = new();

    public ApplicationServices(ITestOutputHelper output)
    {
        _output = output;

        var args = new string[] { "", "" };
        _builder = Host.CreateApplicationBuilder(args);
        _builder.Services.AddServices();

        //load our inmemory config for testing before we build services
        LoadTestConfigs();

        _host = _builder.Build();
    }

    internal void log(object? message)
    {
        var msg = Convert.ToString(message) ?? "Message was null!";
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
        _output.WriteLine(msg);
    }

    private void LoadTestConfigs()
    {
        var mediaConfig = new MediaCollectionConfiguration
        {
            DirectoryRecursionDepth = 50,
            BaseDirectory = "testMedia",
            Filter = "*.avi;*.mkv",
        };

        _builder.Services.AddSingleton(Options.Create(mediaConfig));

        var databaseConfig = new DatabaseConfiguration
        {
            DataFile = "testdata/appservices.db",
        };

        var thesaurusConfig = new ThesaurusConfiguration()
        {
            DictionaryPath = "wordnet/staticdata/",
            DatabasePath = "wordnet/database/wordnet.db"
        };

        _builder.Services.AddSingleton(Options.Create(databaseConfig))
                        .AddSingleton(Options.Create(thesaurusConfig))
                        .Configure<shared.EndpointConfig>(_builder.Configuration.GetSection("TMDB"));
    }

    [Fact]
    public void ServicesRegistered()
    {
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMediaService()
    {
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchBasic()
    {
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        var config = _host.Services.GetRequiredService<IOptions<MediaCollectionConfiguration>>();

        config.Should().NotBeNull();

        await CreateTestFile(10, 500);

        CancellationTokenSource source = new();

        await media.UpdateRepos(null, false, source.Token);

        var pattern = @".+\.avi";

        var searchResult = await media.Search([pattern], source.Token);

        searchResult.Should().NotBeNull();

        searchResult.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchMultiplePatterns()
    {

        await CreateTestFile(20, 250, (byte)'a', "avi");
        await CreateTestFile(20, 250, (byte)'m', "mp3");

        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        CancellationTokenSource source = new();

        await media.UpdateRepos(null, false, source.Token);

        var patterns = @".+\.avi$;\.mp3$.+\.mkv".Split(';');

        var searchResults = await media.Search(patterns, source.Token);

        var searchResult = searchResults.ToList().SelectMany(x => x);

        searchResult.Should().NotBeNull();

        searchResult.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetFileInfo()
    {
        var config = _host.Services.GetRequiredService<IOptions<MediaCollectionConfiguration>>();

        config.Should().NotBeNull();

        await CreateTestFile(1);

        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        await media.UpdateRepos(config.Value.BaseDirectory, true, _tokenSource.Token);

        var patterns = @"\.avi$;.+\.mkv".Split(';');

        var results = (await media.Search(patterns, _tokenSource.Token)).ToList();

        results.Should().NotBeEmpty();

        var info = media.GetFile(results.ToList()[0]);

        info.Should().NotBeNull();

        _output.WriteLine(info.ToString());

    }

    [Fact]
    public async Task GetMovieDetails()
    {
        var client = _host.Services.GetRequiredService<shared.TMDB.ITMDB>();

        var movieDetails = await client.GetMovie(11);

        movieDetails.Should().NotBeNull();

        movieDetails.adult.Should().BeFalse();

        movieDetails.budget.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QueryMovieAndDetails()
    {
        var client = _host.Services.GetRequiredService<shared.TMDB.ITMDB>();

        var matches = await client.QueryTitle("Star Wars");

        matches.Should().NotBeNull();

        matches.results.Should().NotBeNull();

        matches.results.Count().Should().BeGreaterThan(0);

        matches.results.ForEach(x => log(x.id.ToString()));

        matches.results[0].id.Should().NotBeNull();

        var movie = client.GetMovie((int)matches.results[0]!.id!);

        movie.Should().NotBeNull();

        log(JsonConvert.SerializeObject(movie));
    }

    [Fact]
    public async Task QueryLocalAndRemoteMovies()
    {
        await CreateTestFile(1, 250, (byte)'w', "avi", "Inglourious Basterds");

        var client = _host.Services.GetRequiredService<shared.TMDB.ITMDB>();

        var movieName = "Inglourious Basterds";

        var remoteMatches = await client.QueryTitle(movieName);

        remoteMatches.Should().NotBeNull();

        remoteMatches.results.Should().NotBeNull();

        remoteMatches.results.Count().Should().BeGreaterThan(0);

        remoteMatches.results.ForEach(x => log(x.id.ToString()));

        remoteMatches.results[0].id.Should().NotBeNull();

        var movie = client.GetMovie((int)remoteMatches.results[0]!.id!);

        movie.Should().NotBeNull();

        log("Remote movie details");

        log(JsonConvert.SerializeObject(movie));

        var media = _host.Services.GetRequiredService<IMediaCollection>();

        await media.Initialize(_tokenSource.Token);

        await media.UpdateRepos(null, true, _tokenSource.Token);

        var keywords = SearchHelpers.SanitizeForSearch(movieName, _tokenSource.Token, false);

        var localMatches = await media.FindInPath<shared.data.File>(keywords, _tokenSource.Token);

        localMatches.Should().NotBeNullOrEmpty();
    }


    private async Task CreateTestFile(int count, long sizeKb = 250, byte filler = (byte)'w', string extension = "avi", string fileName = null)
    {
        var config = _host.Services.GetRequiredService<IOptions<MediaCollectionConfiguration>>();

        config.Should().NotBeNull();

        var fileDirectory = Path.GetFullPath(config.Value.BaseDirectory);

        for (int i = 0; i < count; i++)
        {
            var name = Path.ChangeExtension(fileName is not null ? fileName : Guid.NewGuid().ToString(), extension);
            var filePath = Path.Combine(fileDirectory, name);
            if (await FileHelper.CreateFile(filePath, sizeKb, filler))
            {
                _filesToDelete.Add(filePath);
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
            _filesToDelete.ForEach(x => _output.WriteLine($"File to delete: {x}"));
            _filesToDelete.ForEach(x => Console.WriteLine($"File to delete: {x}"));
            _filesToDelete.ForEach(System.IO.File.Delete);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to clean up a test artifact: {ex}");
            throw;
        }
    }
    #endregion IDisposable
}