using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using shared.data;

namespace shared;

public interface IMediaCollection
{
    Task Initialize(CancellationToken token);
    Task Clear(CancellationToken token);
    Task<shared.data.File?> GetFile(string filePath);
    Task UpdateRepos(string baseDirectory, CancellationToken token);
    Task<IEnumerable<string>> Search(string pattern, CancellationToken token);
}

public class MediaCollection : IMediaCollection
{
    private readonly MediaReaderConfiguration _configuration;

    private Dictionary<string, shared.data.File> _mediaRepo;
    private Dictionary<string, DirectoryInfo> _directoryRepo;

    private CancellationTokenSource _tokenSource = new();

    private shared.data.IDatabase _db;

    public MediaCollection(IOptions<MediaReaderConfiguration> configuration, IDatabase db)
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
            _directoryRepo = Directory.EnumerateDirectories(mediaDirectory)
                .AsParallel().WithCancellation(token)
                .ToDictionary(x => x, x => new DirectoryInfo(x));

            // add the base directory to include files in it
            _directoryRepo.Add(mediaDirectory, new DirectoryInfo(mediaDirectory));

            var fileList = _directoryRepo.Keys
                .AsParallel().WithCancellation(token)
                .SelectMany(x => Directory.EnumerateFiles(x)).ToList();

            var filesTaks = fileList.Select(async x => await FileHelper.PathToFile(x, _tokenSource.Token)).ToList();

            var filesData = await Task.WhenAll(filesTaks);

            _mediaRepo = filesData.ToDictionary(x => x.path, x => x);

            _db.EnsureConnected();

            await _db.Truncate();

            await _db.InsertOrUpdate(filesData);

            _mediaRepo = (await _db.Files()).ToDictionary(x => x.path, x => x);

            _db.Disconnect();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    public async Task<IEnumerable<string>> Search(string pattern, CancellationToken token)
    {
        if (string.IsNullOrEmpty(pattern)) throw new ArgumentNullException($"Pattern is null or empty: {pattern}");

        if (_mediaRepo is null || !_mediaRepo.Any())
        {
            return Enumerable.Empty<string>();
        }

        var filtered = _mediaRepo
                .AsParallel().WithCancellation(token)
                .Where(x => Regex.IsMatch(Path.GetFileName(x.Value.path), pattern, RegexOptions.IgnoreCase)).ToList();

        if (!filtered.Any())
        {
            return Enumerable.Empty<string>();
        }

        return filtered.Select(x => x.Key).ToImmutableList();
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