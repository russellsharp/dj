using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using shared;
using FluentAssertions;

namespace dj.test;

public class BaseTest : IDisposable
{
    public ITestOutputHelper _output;
    private List<string> _filesToDelete = new();
    private string ReferenceDataFile = "data/media.db";
    private string ReferenceTmdbDataFile = "data/tmdb.db";
    protected CancellationTokenSource _cts = new();
    public string ReferenceDatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, ReferenceDataFile));
        }
    }

    public string ReferenceTmdbDatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, ReferenceTmdbDataFile));
        }
    }

    public BaseTest(ITestOutputHelper output)
    {
        _output = output;
    }

    protected void log(object? message, [CallerMemberName] string caller = "")
    {
        var msg = $"[{caller}] - {Convert.ToString(message) ?? "Message was null!"}";
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
        _output.WriteLine(msg);
    }

    protected async Task CreateTestFile(string parentDirectory, int count, long sizeKb = 250, byte filler = (byte)'w', string extension = "avi", string? fileName = null)
    {
        var fileDirectory = Path.GetFullPath(parentDirectory);

        for (int i = 0; i < count; i++)
        {
            var name = Path.ChangeExtension(fileName ?? Guid.NewGuid().ToString(), extension);
            var filePath = Path.Combine(fileDirectory, name);
            if (await FileHelper.CreateFile(filePath, sizeKb, filler))
            {
                _filesToDelete.Add(filePath);
            }
        }
    }

    #region IDisposable
    private int _disposed = 0;
    public void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            // _filesToDelete.ForEach(x => log($"File to delete: {x}"));
            _filesToDelete.ForEach(System.IO.File.Delete);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to clean up a test artifact: {ex}");
            throw;
        }
    }
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~BaseTest()
    {
        Dispose(false);
    }
    #endregion IDisposable
}