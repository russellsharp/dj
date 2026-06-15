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
            DataFile = "testdatabase/testmedia.db",
        };

        _builder.Services.AddSingleton(Options.Create(databaseConfig));
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

        var searchResult = await media.Search(pattern, source.Token);

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

        var searchTasks = patterns?.Select(async x => await media.Search(x, source.Token)) ?? Enumerable.Empty<Task<IEnumerable<string>>>();

        var searchResults = await Task.WhenAll(searchTasks);

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

        var searchTasks = patterns?.Select(async x => await media.Search(x, source.Token)) ?? Enumerable.Empty<Task<IEnumerable<string>>>();

        var results = await Task.WhenAll(searchTasks);

        var searchResult = results.ToList().SelectMany(x => x).ToList();

        searchResult.Should().NotBeEmpty();

        var info = media.GetFile(searchResult[0]);

        info.Should().NotBeNull();

        _output.WriteLine(info.ToString());

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