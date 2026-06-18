using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using System.Net.Mail;
using System.Diagnostics;
using SQLitePCL;
using Microsoft.Extensions.Options;
using shared.data;
using Newtonsoft.Json;

namespace dj.test;

public class ApplicationServices : IDisposable
{

    private HostApplicationBuilder _builder;
    private IHost _host;
    private ITestOutputHelper _output;
    private List<string> _filesToDelete = new();

    private const string testfileDirectory = "testfiles";
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
        var mediaConfig = new MediaReaderConfiguration
        {
            DirectoryRecursionDepth = 50,
            BaseDirectory = "testMedia",
            Filter = "*.avi",
        };

        _builder.Services.AddSingleton(Options.Create(mediaConfig));

        var databaseConfig = new DatabaseConfiguration
        {
            DataFile = "testdata/testmedia.db",
        };

        _builder.Services.AddSingleton(Options.Create(databaseConfig))
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

        await CreateTestFile(10, 500);

        CancellationTokenSource source = new();

        await media.UpdateRepos(testfileDirectory, source.Token);

        var pattern = @".+\.avi$";

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

        await media.UpdateRepos(testfileDirectory, source.Token);

        var patterns = @".+\.avi$;\.mp3$".Split(';');

        var searchResults = await media.Search(patterns, source.Token);

        var searchResult = searchResults.ToList().SelectMany(x => x);

        searchResult.Should().NotBeNull();

        searchResult.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetFileInfo()
    {
        await CreateTestFile(1);

        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        CancellationTokenSource source = new();

        await media.UpdateRepos(testfileDirectory, source.Token);

        var patterns = @"\.avi$".Split(';');

        var results = await media.Search(patterns, source.Token);

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

        var matches = await client.QueryMovies("Star Wars");

        matches.Should().NotBeNull();

        matches.results.Should().NotBeNull();

        matches.results.Count().Should().BeGreaterThan(0);

        matches.results.ForEach(x => log(x.id.ToString()));

        matches.results[0].id.Should().NotBeNull();

        var movie = client.GetMovie((int)matches.results[0]!.id!);

        movie.Should().NotBeNull();

        log(JsonConvert.SerializeObject(movie));
    }


    private async Task CreateTestFile(int count, long sizeKb = 250, byte filler = (byte)'w', string extension = "avi")
    {
        var config = _host.Services.GetRequiredService<IOptions<MediaReaderConfiguration>>();

        config.Should().NotBeNull();

        var fileDirectory = Path.GetFullPath(config.Value.BaseDirectory);

        for (int i = 0; i < count; i++)
        {
            var fileName = Path.ChangeExtension($"{Guid.NewGuid()}", extension);
            var filePath = Path.Combine(fileDirectory, fileName);
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
            _filesToDelete.ForEach(x => System.IO.File.Delete(x));
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to clean up a test artifact: {ex}");
            throw;
        }
    }
    #endregion IDisposable
}