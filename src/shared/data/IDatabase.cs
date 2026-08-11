using System.Runtime.CompilerServices;

namespace shared.data;

public interface IMediaDatabase
{
    void Connect([CallerMemberName] string caller = "");
    Task Create(CancellationToken? token = null, [CallerMemberName] string caller = "");
    Task Truncate(CancellationToken? token = null, [CallerMemberName] string caller = "");
    void Dispose(bool disposing);
    void Dispose();
    Task<File?> File(string path, CancellationToken? token = null);
    Task<bool> FileExists(string filePath, CancellationToken? token = null);
    Task<IEnumerable<File>> Files(CancellationToken? token = null);
    Task<IEnumerable<File>> Files(IEnumerable<string> paths, CancellationToken? token = null);
    Task<IEnumerable<File>> FilesByDirectory(IEnumerable<string> paths, CancellationToken? token = null);
    Task<IEnumerable<File>> FilesByExtensions(IEnumerable<string> extensions, CancellationToken? token = null);
    Task Insert(File file, CancellationToken? token = null);
    Task InsertOrUpdate(IEnumerable<File> testData, CancellationToken? token = null);
}
