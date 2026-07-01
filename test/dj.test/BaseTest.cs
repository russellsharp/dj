using System.Diagnostics;
using shared;

namespace dj.test;

public class BaseTest : IDisposable
{
    public ITestOutputHelper _output;
    private List<string> _filesToDelete = new();
    public CancellationTokenSource _tokenSource = new();

    public BaseTest(ITestOutputHelper output)
    {
        _output = output;
    }

    protected void log(object? message)
    {
        var msg = Convert.ToString(message) ?? "Message was null!";
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