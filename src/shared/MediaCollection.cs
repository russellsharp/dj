using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using shared.data;
using shared.TMDB;

namespace shared;


public enum MediaType
{
    Video,
    Audio
}

public interface IMediaCollection
{
    Task Initialize(CancellationToken token);
    Task Clear(CancellationToken token);
    Task UpdateRepos(string? baseDirectory = null, bool truncateDatabase = false, CancellationToken? token = null);
    Task<shared.data.File?> File(string filePath);
    Task<IEnumerable<shared.data.File>> Files(MediaType type);
    Task<IEnumerable<string>> Search(IEnumerable<string> patterns, CancellationToken? token);
    Task<IEnumerable<MatchScore<ResponseType>>> FindInPath<ResponseType>(IEnumerable<string> keywords, int? minimumHits = null, CancellationToken? token = null) where ResponseType : class;
}

public class MediaCollection : IMediaCollection
{
    private readonly MediaCollectionConfiguration _configuration;

    private Dictionary<string, shared.data.File> _mediaRepo;
    private Dictionary<string, DirectoryInfo> _directoryRepo;

    private CancellationTokenSource _cts;

    private shared.data.IDatabase _db;

    public MediaCollection(IOptions<MediaCollectionConfiguration> configuration, IDatabase db, ITMDB tmdb, CancellationTokenSource cts)
    {
        _configuration = configuration.Value;

        _mediaRepo = [];

        _directoryRepo = [];

        _db = db;

        _cts = cts;
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

    public async Task UpdateRepos(string? baseDirectory = null, bool truncateDatabase = false, CancellationToken? token = null)
    {
        token ??= _cts.Token;


        var mediaDirectory = !string.IsNullOrEmpty(baseDirectory) ? Path.GetFullPath(baseDirectory) : Path.GetFullPath(_configuration.BaseDirectory);

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = _configuration.DirectoryRecursionDepth
        };

        if (!Directory.Exists(mediaDirectory))
        {
            _mediaRepo = new Dictionary<string, data.File>();
            return;
        }

        try
        {
            _directoryRepo = Directory.EnumerateDirectories(mediaDirectory, "*", SearchOption.AllDirectories).ToDictionary(x => x, x => new DirectoryInfo(x));
            _directoryRepo.Add(mediaDirectory, new DirectoryInfo(mediaDirectory));

            //TODO: update files filtered by updated directory time

            List<string> extensions = _configuration.VideoExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            extensions.AddRange(_configuration.AudioExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            extensions = extensions.Select(x =>
            {
                //get file extension leaves the . prefixed.
                if (x[0] != '.') return '.' + x.ToLower();
                else { return x.ToLower(); }
            }).ToList();

            var fileList = Directory.EnumerateFiles(mediaDirectory, "*", SearchOption.AllDirectories).Where(x => extensions.Contains(Path.GetExtension(x).ToLower())).ToList();

            _db.EnsureConnected();

            if (truncateDatabase)
            {
                await _db.Truncate();
            }


            // var paralllelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = token.Value };
            // await Parallel.ForEachAsync(fileList, paralllelOptions, async (file, ct) => { await ProcessFile(file, token.Value); });
            var filesTasks = fileList.Select(async x => await ProcessFile(Path.GetFullPath(x), token.Value)).ToList();
            await Task.WhenAll(filesTasks);

            //files remaining to store
            Debug.WriteLine($"Remaining files to store: {_filesToStore.Count()}");

            {
                try
                {
                    Monitor.Enter(_processFileQueueLock);
                    await _db.InsertOrUpdate(_filesToStore);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error while calling InsertOrUpdate: {ex}");
                    throw;
                }
                finally
                {
                    if (Monitor.IsEntered(_processFileQueueLock))
                    {
                        Monitor.Exit(_processFileQueueLock);
                    }
                }
            }
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
    private readonly object _processFileQueueLock = new();
    private static int _fileCount = 0;

    public async Task ProcessFile(string filePath, CancellationToken token)
    {
        if (!await _db.FileExists(Path.GetFullPath(filePath)))
        {
            var newFile = await FileHelper.PathToFile(filePath);
            _filesToStore.Add(newFile);
            int count = Interlocked.Increment(ref _fileCount);
            Debug.WriteLine($"Files processed: {count}, bag: {_filesToStore.Count()}");
        }

        if (_filesToStore.Count() > MaximumBagSize)
        {
            if (Monitor.TryEnter(_processFileQueueLock))
            {
                try
                {
                    Debug.WriteLine($"Files stored: {_filesToStore.Count()}");
                    await _db.InsertOrUpdate(_filesToStore);
                    _filesToStore.Clear();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error while calling InsertOrUpdate: {ex}");
                    throw;
                }
                finally
                {
                    if (Monitor.IsEntered(_processFileQueueLock))
                    {
                        Monitor.Exit(_processFileQueueLock);
                    }
                }
            }
        }

    }

    public async Task<IEnumerable<string>> Search(IEnumerable<string> patterns, CancellationToken? token)
    {
        token ??= _cts.Token;

        if (patterns is null || !patterns.Any()) throw new ArgumentException($"Patterns are empty or null.");

        patterns = patterns.Select(x => x.ToLowerInvariant());

        if (_mediaRepo is null || !_mediaRepo.Any())
        {
            return Enumerable.Empty<string>();
        }

        var filtered = new List<string>();

        // _mediaRepo.Keys.ToList().ForEach(x => Debug.WriteLine(x));
        foreach (var pattern in patterns)
        {
            filtered.AddRange(_mediaRepo.Keys
                    .AsParallel().WithCancellation(token.Value)
                    .Where(x => Regex.IsMatch(x, pattern, RegexOptions.IgnoreCase)));
        }

        return filtered.Distinct().ToImmutableList();
    }

    public async Task<IEnumerable<MatchScore<ContainedType>>> FindInPath<ContainedType>(IEnumerable<string> keywords, int? minimumHits = null, CancellationToken? token = null) where ContainedType : class
    {
        minimumHits ??= keywords.Count();

        token ??= _cts.Token;

        if (!keywords.Any()) throw new ArgumentNullException("Keywords are empty");

        if (_mediaRepo is null || !_mediaRepo.Any())
        {
            return Enumerable.Empty<MatchScore<ContainedType>>();
        }

        var scoredMatches = new Dictionary<string, MatchScore<ContainedType>>();

        foreach (var file in _mediaRepo.Values)
        {
            foreach (var keyword in keywords.Select(x => x.ToLower()))
            {
                if (file.path.ToLower().Contains(keyword))
                {
                    if (scoredMatches.ContainsKey(file.path))
                    {
                        scoredMatches[file.path].Hits++;
                    }
                    else
                    {
                        scoredMatches.Add(file.path, new MatchScore<ContainedType>() { Hits = 1, Details = file as ContainedType });
                    }
                }
            }
        }

        return scoredMatches.Values.Where(x => x.Hits >= minimumHits);
    }

    public async Task<shared.data.File?> File(string filePath)
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

    public async Task<IEnumerable<data.File>> Files(MediaType type)
    {
        var requestExtensions = type switch
        {
            MediaType.Audio => _configuration.AudioExtensions.Split(';').Select(x => { if (x[0] != '.') { return $".{x}"; } else { return x; } }),
            MediaType.Video => _configuration.VideoExtensions.Split(';').Select(x => { if (x[0] != '.') { return $".{x}"; } else { return x; } }),
            _ => ["*"]
        };

        var files = _mediaRepo.Values.Where(x => requestExtensions.Contains(Path.GetExtension(x.path)));

        return files;
    }
}