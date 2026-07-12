using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using shared.data;
using shared.TMDB;
using SQLitePCL;

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
    UpdateStatus Status { get; }
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
        _db.Connect();

        _db.Create();

        await LoadDatabase(token);
    }

    public async Task Clear(CancellationToken token)
    {
        await _db.Truncate();

        _mediaRepo.Clear();
    }

    private async Task LoadDatabase(CancellationToken token)
    {
        var fileData = await _db.Files();

        _mediaRepo = fileData.ToDictionary(x => x.path, x => x);
    }

    private SemaphoreSlim _dbSemaphore = new(1, 1);
    private ConcurrentQueue<shared.data.File> _filesToStore = new();
    private static long _updateState = 0;
    private static long _numOfFilesTotal = 0;
    private static long _numOfFilesProcessed = 0;
    private static long _filesQueued = 0;
    private const int MaximumQueueSize = 20;

    public UpdateStatus Status
    {
        get
        {
            return new UpdateStatus
            {
                State = (UpdateState)Interlocked.Read(ref _updateState),
                FilesProcessed = Interlocked.Read(ref _numOfFilesProcessed),
                TotalFiles = Interlocked.Read(ref _numOfFilesTotal)
            };
        }
    }

    public async Task UpdateRepos(string? baseDirectory = null, bool truncateDatabase = false, CancellationToken? token = null)
    {
        var startTime = Stopwatch.GetTimestamp();

        token ??= _cts.Token;

        try
        {
            if ((UpdateState)Interlocked.Read(ref _updateState) == UpdateState.Running)
            {
                Console.WriteLine("Media repo update requested while update is already running.");
                return;
            }

            //set flag for in progress
            Interlocked.Exchange(ref _updateState, (int)UpdateState.Running);

            var fileList = (await BuildRepoList(baseDirectory)).ToList();

            Console.WriteLine($"Total files: {fileList.Count()}");

            Interlocked.Exchange(ref _numOfFilesTotal, fileList.Count);

            if (truncateDatabase)
            {
                Console.WriteLine("Truncating database.");
                await _db.Truncate();
            }

            Console.WriteLine("Starting update tasks.");
            var paralllelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = token.Value };
            await Parallel.ForEachAsync(fileList, paralllelOptions, async (file, ct) => { await ProcessFile(file, token.Value); });
            Console.WriteLine("Finished update tasks.");

            Console.WriteLine($"Remaining files to store: {_filesToStore.Count}");

            //files remaining to store
            await InsertOrUpdateFIles(token);

            _mediaRepo = (await _db.Files()).ToDictionary(x => x.path, x => x);

            Interlocked.Exchange(ref _updateState, (int)UpdateState.Complete);
        }
        catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
        {
            Console.WriteLine("Update process canceled.");
            Interlocked.Exchange(ref _updateState, (int)UpdateState.Canceled);
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception thrown during update: {ex}");
            Interlocked.Exchange(ref _updateState, (int)UpdateState.Errored);
        }
        finally
        {
            Console.WriteLine("Update completed.");

            Interlocked.Exchange(ref _numOfFilesProcessed, 0);
            Interlocked.Exchange(ref _numOfFilesTotal, 0);
            Interlocked.Exchange(ref _filesQueued, 0);
            _filesToStore.Clear();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);
            Console.WriteLine($"Time for update: {Stopwatch.GetElapsedTime(startTime).ToString("c")}");
        }
    }

    private async Task ProcessFile(string filePath, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            token?.ThrowIfCancellationRequested();

            if (!await _db.FileExists(Path.GetFullPath(filePath)))
            {
                var newFile = await Task.Run(() => FileHelper.PathToFile(filePath));
                _filesToStore.Enqueue(newFile);
                Interlocked.Increment(ref _filesQueued);
            }
            else
            {
                //file is in the database and so is fully processed
                Interlocked.Increment(ref _numOfFilesProcessed);
            }

            if (Interlocked.Read(ref _filesQueued) > MaximumQueueSize)
            {
                await InsertOrUpdateFIles(token);
            }

        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
        {
            throw;
        }
    }

    private async Task InsertOrUpdateFIles(CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            await _dbSemaphore.WaitAsync();

            token?.ThrowIfCancellationRequested();
            var files = new List<data.File>();
            while (_filesToStore.TryDequeue(out var fileToStore) && !token!.Value.IsCancellationRequested)
            {
                files.Add(fileToStore);
            }
            await _db.InsertOrUpdate(files, token);
            Interlocked.Add(ref _numOfFilesProcessed, files.Count);
            Interlocked.Exchange(ref _filesQueued, 0);
            token?.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
        {
            Console.WriteLine("Update process canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while calling InsertOrUpdate: {ex}");
            throw;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private async Task<IEnumerable<string>> BuildRepoList(string? baseDirectory)
    {
        var mediaDirectory = !string.IsNullOrEmpty(baseDirectory) ? baseDirectory : _configuration.BaseDirectory;

        mediaDirectory = Path.GetFullPath(mediaDirectory);

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = _configuration.DirectoryRecursionDepth
        };

        if (!Directory.Exists(mediaDirectory))
        {
            _mediaRepo = new Dictionary<string, data.File>();
            return Enumerable.Empty<string>();
        }
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

        return Directory.EnumerateFiles(mediaDirectory, "*", SearchOption.AllDirectories).Where(x => extensions.Contains(Path.GetExtension(x).ToLower())).ToList();
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
            MediaType.Audio => _configuration.AudioExtensions.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => { if (x[0] != '.') { return $".{x}"; } else { return x; } }),
            MediaType.Video => _configuration.VideoExtensions.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => { if (x[0] != '.') { return $".{x}"; } else { return x; } }),
            _ => ["*"]
        };

        var files = _mediaRepo.Values.Where(x => requestExtensions.Contains(Path.GetExtension(x.path)));

        return files;
    }
}