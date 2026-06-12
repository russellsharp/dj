using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using System.Net.Mail;
using System.Diagnostics;
using SQLitePCL;

namespace dj;

public class ApplicationServicesTest : IDisposable
{

    private HostApplicationBuilder _builder;

    private IHost _host;
    private ITestOutputHelper _output;
    private List<string> _filesToDelete = new();

    public ApplicationServicesTest(ITestOutputHelper output)
    {
        _output = output;

        var args = new string[] { "", "" };
        _builder = Host.CreateApplicationBuilder(args);
        _builder.AddConfiguration()
            .AddServices();

        //load our inmemory config before we build services
        LoadConfig();

        _host = _builder.Build();

        CreateTestFile();
    }

    private void LoadConfig()
    {
        string baseDir = AppContext.BaseDirectory;

        var inlineData = new Dictionary<string, string?>
        {
            { "BaseDirectory", "testData" },
            { "Filter", "*.avi" },
            { "DirectoryRecursionDepth", "50" }
        };

        _builder.Configuration.AddInMemoryCollection(inlineData);

        _builder.Services.Configure<MediaReaderConfiguration>(_builder.Configuration);
    }

    private void CreateTestFile()
    {
        var config = _builder.Configuration.Get<MediaReaderConfiguration>();

        config.Should().NotBeNull();

        var fileDirectory = Path.GetFullPath(config.BaseDirectory);

        var filePath = Path.Combine(fileDirectory, $"{Guid.NewGuid()}.avi");

        fileDirectory.Should().NotBeNullOrEmpty();

        Directory.CreateDirectory(fileDirectory);

        using var file = File.CreateText(filePath);

        file.WriteLine("test file to be deleted");

        File.Exists(filePath).Should().BeTrue();

        _filesToDelete.Add(filePath);
    }

    [Fact]
    public void ServicesRegistered()
    {
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchBasic()
    {
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        CancellationTokenSource source = new();

        await media.Populate(source.Token);

        var pattern = @".+\.avi$";

        var searchResult = await media.Search(pattern, source.Token);

        searchResult.Should().NotBeNull();

        searchResult.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchMultiplePatterns()
    {
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        CancellationTokenSource source = new();

        await media.Populate(source.Token);

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
        var media = _host.Services.GetService<IMediaCollection>();

        media.Should().NotBeNull();

        CancellationTokenSource source = new();

        await media.Populate(source.Token);

        var patterns = @"\.avi$".Split(';');

        var searchTasks = patterns?.Select(async x => await media.Search(x, source.Token)) ?? Enumerable.Empty<Task<IEnumerable<string>>>();

        var results = await Task.WhenAll(searchTasks);

        var searchResult = results.ToList().SelectMany(x => x).ToList();

        searchResult.Should().NotBeEmpty();

        var info = media.GetFile(searchResult[0]);

        info.Should().NotBeNull();

        _output.WriteLine(info.ToString());
        // info.CreationTime.Should().Be

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
            _filesToDelete.ForEach(x => _output.WriteLine(x));
            _filesToDelete.ForEach(x => Debug.WriteLine($"File to delete: {x}"));
            _filesToDelete.ForEach(x => File.Delete(x));
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to clean up a test artifact: {ex}");
            throw;
        }
        finally
        {
        }
    }
    #endregion IDisposable
}