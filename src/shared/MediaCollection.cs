using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using shared.data;
using shared.TMDB;
using shared.TMDB.Models;
using SQLitePCL;
using WeCantSpell.Hunspell;

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
    Task UpdateRepos(string baseDirectory, bool truncateDatabase = false, CancellationToken? token = null);
    Task<shared.data.File?> GetFile(string filePath);
    Task<IEnumerable<shared.data.File>> Files(MediaType type);
    Task<IEnumerable<string>> Search(IEnumerable<string> patterns, CancellationToken? token);
    Task<IEnumerable<MatchScore<ResponseType>>> FindInPath<ResponseType>(IEnumerable<string> keywords, CancellationToken? token) where ResponseType : class;
}

public class MediaCollection : IMediaCollection
{
    private readonly MediaReaderConfiguration _configuration;

    private Dictionary<string, shared.data.File> _mediaRepo;
    private Dictionary<string, DirectoryInfo> _directoryRepo;

    private CancellationTokenSource _tokenSource = new();

    private shared.data.IDatabase _db;

    public MediaCollection(IOptions<MediaReaderConfiguration> configuration, IDatabase db, ITMDB tmdb)
    {
        _configuration = configuration.Value;

        _mediaRepo = [];

        _directoryRepo = [];

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

    public async Task UpdateRepos(string baseDirectory, bool truncateDatabase = false, CancellationToken? token = null)
    {
        token ??= _tokenSource.Token;

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
            _directoryRepo = Directory.EnumerateDirectories(_configuration.BaseDirectory, "*", SearchOption.AllDirectories).ToDictionary(x => x, x => new DirectoryInfo(x));
            _directoryRepo.Add(_configuration.BaseDirectory, new DirectoryInfo(_configuration.BaseDirectory));

            List<string> extensions = _configuration.VideoExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            extensions.AddRange(_configuration.AudioExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            extensions = extensions.Select(x =>
            {
                //get file extension leaves the . prefixed.
                if (x[0] != '.') return '.' + x.ToLower();
                else { return x.ToLower(); }
            }).ToList();

            var fileList = Directory.EnumerateFiles(mediaDirectory, "*", SearchOption.AllDirectories).Where(x => extensions.Contains(Path.GetExtension(x).ToLower()));

            _db.EnsureConnected();

            if (truncateDatabase)
            {
                await _db.Truncate();
            }

            var filesTasks = fileList.Select(async x => await ProcessFile(x, _tokenSource.Token)).ToList();

            //process all files in ~MaximumBagSize chunks
            await Task.WhenAll(filesTasks);

            //files remaining to store
            Debug.WriteLine($"Remaining files to store: {_filesToStore.Count()}");

            {
                try
                {
                    Monitor.Enter(_processFileQueueLock);
                    await _db.InsertOrUpdate(_filesToStore);
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
    private Lock _processFileQueueLock = new();
    private static int _fileCount = 0;

    public async Task ProcessFile(string filePath, CancellationToken token)
    {
        if (!await _db.FileExists(filePath))
        {
            var newFile = await FileHelper.PathToFile(filePath, token);
            _filesToStore.Add(newFile);
            int count = Interlocked.Increment(ref _fileCount);
            Debug.WriteLine($"Files processed: {count}");
        }

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

    public async Task<IEnumerable<string>> Search(IEnumerable<string> patterns, CancellationToken? token)
    {
        token ??= _tokenSource.Token;

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
                    .AsParallel().WithCancellation(token.Value)
                    .Where(x => Regex.IsMatch(x, pattern, RegexOptions.IgnoreCase)));
        }

        return filtered.ToImmutableList();
    }

    public async Task<IEnumerable<MatchScore<ContainedType>>> FindInPath<ContainedType>(IEnumerable<string> keywords, CancellationToken? token = null) where ContainedType : class
    {
        token ??= _tokenSource.Token;

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

        // var searchTerm = new string(string.Join(' ', keywords)).ToLower();
        // var scoredMatches = _mediaRepo.Values.Select(x => new MatchScore<ContainedType> { Details = x as ContainedType, Hits = SearchHelpers.Levenshtein(x.path, searchTerm) });
        // return scoredMatches.Where(x => x.Hits <= 60).OrderBy(x => x.Hits);
        return scoredMatches.Values.Where(x => x.Hits >= keywords.Count());
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