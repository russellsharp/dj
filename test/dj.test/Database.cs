
using Xunit.Internal;
using shared.data;
using FluentAssertions;
using shared;
using System.Diagnostics;
using Microsoft.Extensions.Options;
namespace dj.test;

public class Database : IDisposable
{
    private shared.data.Database _db;

    private ITestOutputHelper _output;

    private shared.data.DatabaseConfiguration _dataConfig;

    private bool _deleteDatabaseFile = true;

    private List<string> _filesToDelete = new();

    private CancellationTokenSource _tokenSource = new();

    public Database(ITestOutputHelper output)
    {
        _output = output;

        _dataConfig = new shared.data.DatabaseConfiguration()
        {
            DataFile = Path.GetFullPath("testdatabase/test.db")
        };

        var optionsConfig = Options.Create(_dataConfig);

        _db = new shared.data.Database(optionsConfig);

        _db.Connect();
        _db.Create();
        _db.Truncate().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Connect()
    {
        var act = () => _db.Connect();
        act.Should().NotThrow();

        _db.IsConnected().Should().BeTrue();
    }

    [Fact]
    public async Task Create()
    {
        _db.EnsureConnected();

        _db.Create();

        System.IO.File.Exists(Path.GetFullPath(_dataConfig.DataFile)).Should().BeTrue();

        _db.IsConnected().Should().BeTrue();
    }

    [Fact]
    public async Task CreateAndTruncateDatabase()
    {
        _db.Connect();
        await _db.Truncate();
        _db.Create();
        await _db.Truncate();
        _db.Disconnect();
    }

    [Fact]
    public async Task SaveFile()
    {
        var testFile = @"testData/test_file_01.avi";

        await CreateTestFile(testFile, 5000, (byte)'w');

        var file = await shared.FileHelper.PathToFile(testFile, _tokenSource.Token);

        await _db.Insert(file);

        _db.Disconnect();
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

        var queriedFiles = await _db.FilesByDirectory([Path.GetFullPath("testdata/")]);

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

        queriedFiles = await _db.FilesByDirectory(["testdata", "Meshuggah"]);

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
        var testFiles = Enumerable.Range(1, count).Select(x => Path.ChangeExtension($"testdata/test_file_{x}", extension));
        testFiles.ForEach(async x => await FileHelper.CreateFile(x, rng.NextInt64(sizeKb), filler));

        var testConversion = testFiles
            .AsParallel().WithCancellation(_tokenSource.Token)
            .Select(async x => await shared.FileHelper.PathToFile(x, _tokenSource.Token)).ToList();
        return await Task.WhenAll(testConversion);
    }

    private async Task CreateTestFile(string path, long sizeKb, byte filler)
    {
        await shared.FileHelper.CreateFile(path, sizeKb, filler);

        _filesToDelete.Add(path);
    }

    #region IDisposable
    private int _disposed = 0;

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            if (disposing)
            {
                _db.Disconnect();

                try
                {
                    //delete files created by test
                    foreach (var file in _filesToDelete)
                    {
                        if (System.IO.File.Exists(file))
                            System.IO.File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine(ex.ToString());
                }

                _db.Disconnect();

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

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Database()
    {
        Dispose(false);
    }
    #endregion IDisposable

}