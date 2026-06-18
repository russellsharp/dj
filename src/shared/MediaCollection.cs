using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using shared.data;
using WeCantSpell.Hunspell;

namespace shared;

public interface IMediaCollection
{
    Task Initialize(CancellationToken token);
    Task Clear(CancellationToken token);
    Task<shared.data.File?> GetFile(string filePath);
    Task UpdateRepos(string baseDirectory, CancellationToken token);
    Task<IEnumerable<string>> Search(IEnumerable<string> patterns, CancellationToken token);
    Task<IEnumerable<shared.data.File>> Match(IEnumerable<string> keywords, CancellationToken token);
}

public class MediaCollection : IMediaCollection
{
    private readonly MediaReaderConfiguration _configuration;

    private Dictionary<string, shared.data.File> _mediaRepo;
    // private Dictionary<string, DirectoryInfo> _directoryRepo;

    private CancellationTokenSource _tokenSource = new();

    private shared.data.IDatabase _db;

    public MediaCollection(IOptions<MediaReaderConfiguration> configuration, IDatabase db)
    {
        _configuration = configuration.Value;

        _mediaRepo = [];

        // _directoryRepo = [];

        _db = db;
    }

    public async Task Initialize(CancellationToken token)
    {
        _db.EnsureConnected();

        _db.Create();

        await LoadDatabase(token);

        _db.Disconnect();
    }

    public async Task Clear(CancellationToken token)
    {
        _db.EnsureConnected();

        await _db.Truncate();

        _mediaRepo.Clear();

        _db.Disconnect();
    }

    private async Task LoadDatabase(CancellationToken token)
    {
        _db.EnsureConnected();

        var fileData = await _db.Files();

        _db.Disconnect();

        _mediaRepo = fileData.ToDictionary(x => x.path, x => x);
    }

    public async Task UpdateRepos(string baseDirectory, CancellationToken token)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = _configuration.DirectoryRecursionDepth
        };

        var mediaDirectory = Path.GetFullPath(_configuration.BaseDirectory);
        if (!Directory.Exists(mediaDirectory))
        {
            _mediaRepo = new Dictionary<string, data.File>();
            return;
        }

        try
        {
            List<string> extensions = _configuration.VideoExtensions.ToLower().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            extensions.AddRange(_configuration.AudioExtensions.ToLower().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            extensions = extensions.Select(x =>
            {
                if (x[0] != '.') return '.' + x;
                else { return x; }
            }).ToList();

            var fileList = Directory.EnumerateFiles(mediaDirectory, "*", SearchOption.AllDirectories).Where(x => extensions.Contains(Path.GetExtension(x).ToLower()));

            _db.EnsureConnected();

            var filesTasks = fileList.Select(async x => await ProcessFile(x, _tokenSource.Token)).ToList();

            //process all files in ~MaximumBagSize chunks
            await Task.WhenAll(filesTasks);

            //files remaining to store
            Debug.WriteLine(_filesToStore.Count());
            await _db.InsertOrUpdate(_filesToStore);
            _filesToStore.Clear();

            _mediaRepo = (await _db.Files()).ToDictionary(x => x.path, x => x);

            _db.Disconnect();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private ConcurrentBag<shared.data.File> _filesToStore = new();
    private int MaximumBagSize = 20;
    private Lock _processFileQueueLock = new();
    private static int _fileCount = 0;

    public async Task ProcessFile(string filePath, CancellationToken token)
    {
        if (!await _db.FileExists(filePath))
        {
            var newFile = await FileHelper.PathToFile(filePath, token);
            _filesToStore.Add(newFile);
        }

        int count = Interlocked.Increment(ref _fileCount);
        Debug.WriteLine($"Files processed: {count}");

        if (_filesToStore.Count() > MaximumBagSize)
        {
            if (Monitor.TryEnter(_processFileQueueLock))
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine($"Files stored: {_filesToStore.Count()}");
                        _db.InsertOrUpdate(_filesToStore);
                        _filesToStore.Clear();
                    }
                    finally
                    {
                        if (Monitor.IsEntered(_processFileQueueLock))
                        {
                            Monitor.Exit(_processFileQueueLock);
                        }
                    }
                }
                , token);
            }
        }

    }

    public async Task<IEnumerable<string>> Search(IEnumerable<string> patterns, CancellationToken token)
    {
        if (patterns is null || !patterns.Any()) throw new ArgumentNullException($"Patterns are empty.");

        if (_mediaRepo is null || !_mediaRepo.Any())
        {
            return Enumerable.Empty<string>();
        }

        var filtered = new List<string>();

        _mediaRepo.Keys.ToList().ForEach(x => Debug.WriteLine(x));
        foreach (var pattern in patterns)
        {
            filtered.AddRange(_mediaRepo.Keys
                    .AsParallel().WithCancellation(token)
                    .Where(x => Regex.IsMatch(x, pattern, RegexOptions.IgnoreCase)));
        }

        return filtered.ToImmutableList();
    }

    public async Task<IEnumerable<shared.data.File>> Match(IEnumerable<string> keywords, CancellationToken token)
    {
        if (!keywords.Any()) throw new ArgumentNullException("Keywords are empty");

        if (_mediaRepo is null || !_mediaRepo.Any())
        {
            return Enumerable.Empty<shared.data.File>();
        }

        var filtered = new List<shared.data.File>();

        foreach (var keyword in keywords)
        {
            filtered.AddRange(_mediaRepo.Values.Where(x => x.path.ToLower().Contains(keyword.ToLower())));
        }

        return filtered.DistinctBy(x => x.path);
    }

    public async Task<shared.data.File?> GetFile(string filePath)
    {
        var availability = FileHelper.CanAccessFile(filePath, FileAccess.Read);

        if (availability != FileAccessResult.Available)
        {
            throw new FileLoadException(availability.AccessMessage(filePath, FileAccess.Read));
        }

        if (_mediaRepo.TryGetValue(filePath, out shared.data.File? file))
        {
            return file;
        }
        else
        {
            throw new FileNotFoundException($"FileInfo was not found: {filePath}");
        }
    }
}