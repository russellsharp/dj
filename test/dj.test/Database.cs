
using Xunit.Internal;
using shared.data;
using FluentAssertions;
using shared;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using System.Net;
namespace dj.test;

public class Database : BaseTest, IDisposable
{
    private shared.data.Database _db;
    private shared.data.DatabaseConfiguration _dataConfig;
    private bool _deleteDatabaseFile = true;
    private string _baseDirectory = "testMedia/";

    public Database(ITestOutputHelper output) : base(output)
    {
        _dataConfig = new shared.data.DatabaseConfiguration()
        {
            DataFile = Path.GetFullPath("testdata/database.db")
        };

        var optionsConfig = Options.Create(_dataConfig);

        _db = new shared.data.Database(optionsConfig, _cts);

        _db.Connect();
        _db.Create();
        _db.Truncate().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Connect()
    {
        var act = () => _db.Connect();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Create()
    {
        _db.Create();

        System.IO.File.Exists(Path.GetFullPath(_dataConfig.DataFile)).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAndTruncateDatabase()
    {
        _db.Connect();
        await _db.Truncate();
        _db.Create();
        await _db.Truncate();
    }

    [Fact]
    public async Task SaveFile()
    {
        var testFile = $"{_baseDirectory}/test_file_01.avi";

        await CreateTestFile(Path.GetDirectoryName(testFile), 5000, (byte)'w', (byte)'w', "avi", Path.GetFileName(testFile));

        var file = await shared.FileHelper.PathToFile(testFile);

        await _db.Insert(file);
    }

    [Fact]
    public async Task QueryFiles()
    {
        var testData = await CreateTestFileSet(300);

        await _db.InsertOrUpdate(testData);

        IEnumerable<shared.data.File> files = await _db.Files();

        files.Should().NotBeNull();

        files.Should().NotBeEmpty();

        files.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TruncateDatabase()
    {
        var testData = await CreateTestFileSet(20);

        await _db.InsertOrUpdate(testData);

        await _db.Truncate();

        var files = await _db.Files();

        files.Count().Should().Be(0);
    }

    [Fact]
    public async Task SaveFilesBatch()
    {
        var testData = await CreateTestFileSet(300);

        await _db.InsertOrUpdate(testData);
    }

    [Fact]
    public async Task SaveFilesBatchForeach()
    {
        var testData = await CreateTestFileSet(300);

        await _db.InsertOrUpdate(testData);
    }

    [Fact]
    public async Task Query()
    {
        var testData = await CreateTestFileSet(10);
        await _db.InsertOrUpdate(testData);

        var rng = new Random();
        var randomFile = testData.ToList()[rng.Next(10)];

        randomFile.Should().NotBeNull();

        var queriedFile = await _db.File(Path.GetFullPath(randomFile.path));

        queriedFile.Should().NotBeNull();

        queriedFile.Should().BeEquivalentTo(randomFile);

        _output.WriteLine(queriedFile.ToString());
    }

    [Fact]
    public async Task QueryMultipleFiles()
    {
        var testData = await CreateTestFileSet(10);
        await _db.InsertOrUpdate(testData);

        var referenceSet = testData.ToList()[3..5];

        var queriedFiles = await _db.Files(referenceSet.Select(x => Path.GetFullPath(x.path)));

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(2);

        foreach (var referenceFile in referenceSet)
        {
            var queriedFiled = queriedFiles.FirstOrDefault(x => x.path_hash == referenceFile.path_hash);
            queriedFiled.Should().NotBeNull();
            queriedFiled.Should().BeEquivalentTo(referenceFile);
        }
    }

    [Fact]
    public async Task QueryByExtension()
    {
        var testData = await CreateTestFileSet(10, ".mp3");
        await _db.InsertOrUpdate(testData);

        var rng = new Random();

        var queriedFiles = await _db.FilesByExtensions([".mp3"]);

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(10);

        foreach (var referenceFile in testData.ToList())
        {
            var match = queriedFiles.FirstOrDefault(x => x.path_hash == referenceFile.path_hash);

            match.Should().NotBeNull();

            match.Should().BeEquivalentTo(referenceFile);
        }

        var testData2 = await CreateTestFileSet(10, "avi");
        await _db.InsertOrUpdate(testData2);

        queriedFiles = await _db.FilesByExtensions([".mp3", ".avi"]);

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(testData.Count() + testData2.Count());


        foreach (var referenceFile in testData.Concat(testData2).ToList())
        {
            var match = queriedFiles.FirstOrDefault(x => x.path_hash == referenceFile.path_hash);

            match.Should().NotBeNull();

            match.Should().BeEquivalentTo(referenceFile);
        }

        queriedFiles = await _db.FilesByExtensions([".hamsandwiches"]);

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(0);
    }

    [Fact]
    public async Task QueryByDirectory()
    {
        var testData = await CreateTestFileSet(10, "mp3");
        await _db.InsertOrUpdate(testData);

        var rng = new Random();

        var queriedFiles = await _db.FilesByDirectory([Path.GetFullPath(_baseDirectory)]);

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(10);

        foreach (var referenceFile in testData.ToList())
        {
            var match = queriedFiles.FirstOrDefault(x => x.path_hash == referenceFile.path_hash);

            match.Should().NotBeNull();

            match.Should().BeEquivalentTo(referenceFile);
        }

        var testData2 = await CreateTestFileSet(10, "avi");
        await _db.InsertOrUpdate(testData2);

        queriedFiles = await _db.FilesByDirectory([_baseDirectory, "Meshuggah"]);

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(testData.Count() + testData2.Count());

        foreach (var referenceFile in testData.Concat(testData2).ToList())
        {
            var match = queriedFiles.FirstOrDefault(x => x.path_hash == referenceFile.path_hash);

            match.Should().NotBeNull();

            match.Should().BeEquivalentTo(referenceFile);
        }

        queriedFiles = await _db.FilesByExtensions([".hamsandwiches"]);

        queriedFiles.Should().NotBeNull();

        queriedFiles.Count().Should().Be(0);
    }

    private async Task<IEnumerable<shared.data.File>> CreateTestFileSet(int count, string extension = "avi", long sizeKb = 500, byte filler = (byte)'w')
    {
        Random rng = new Random();
        var testFiles = Enumerable.Range(1, count).Select(x => Path.ChangeExtension($"{_baseDirectory}/{Guid.NewGuid().ToString()}", extension)).ToList();
        var fileCreation = testFiles.Select(async x => await CreateTestFile(Path.GetDirectoryName(x), 1, rng.NextInt64(sizeKb), filler, extension, Path.GetFileName(x))).ToList();

        await Task.WhenAll(fileCreation);

        var testFileData = testFiles
            .AsParallel().WithCancellation(_cts.Token)
            .Select(async x => await shared.FileHelper.PathToFile(Path.GetFullPath(x))).ToList();
        return await Task.WhenAll(testFileData);
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

                if (_deleteDatabaseFile && System.IO.File.Exists(_dataConfig.DataFile))
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
                            System.IO.File.Delete(_dataConfig.DataFile);
                            break;
                        }
                        catch
                        {
                            Task.Delay(TimeSpan.FromSeconds(1).Milliseconds);
                            tries++;
                        }
                    }
                }
            }
        }
    }
    #endregion IDisposable

}