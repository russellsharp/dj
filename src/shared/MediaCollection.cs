using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace shared;

public interface IMediaCollection
{
    FileInfo? GetFile(string filePath);
    Task Populate(CancellationToken token);
    Task<IEnumerable<string>> Search(string pattern, CancellationToken token);
}

public class MediaCollection : IMediaCollection
{
    private readonly MediaReaderConfiguration _configuration;

    private Dictionary<string, FileInfo> _mediaRepo;

    private Dictionary<string, DirectoryInfo> _directoryRepo;

    public MediaCollection(IOptions<MediaReaderConfiguration> configuration)
    {
        _configuration = configuration.Value;

        _mediaRepo = [];

        _directoryRepo = [];
    }

    public async Task Populate(CancellationToken token)
    {
        await GetRepos(token);
    }

    private async Task GetRepos(CancellationToken token)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = _configuration.DirectoryRecursionDepth
        };

        var mediaDirectory = Path.GetFullPath(_configuration.BaseDirectory);
        if (!Directory.Exists(mediaDirectory))
        {
            throw new ArgumentException($"Configured basedirectory does not exist: \r\n\t\t {mediaDirectory}");
        }

        try
        {
            _directoryRepo = Directory.EnumerateDirectories(mediaDirectory)
                .AsParallel().WithCancellation(token)
                .ToDictionary(x => x, x => new DirectoryInfo(x));

            // add the base directory to include files in it
            _directoryRepo.Add(mediaDirectory, new DirectoryInfo(mediaDirectory));

            _mediaRepo = _directoryRepo.Keys
                .AsParallel().WithCancellation(token)
                .SelectMany(x => Directory.EnumerateFiles(x))
                .ToDictionary(x => x, x => new FileInfo(x));
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

        var filtered = _mediaRepo.AsParallel().WithCancellation(token).Where(x => Regex.IsMatch(x.Value.Name, pattern, RegexOptions.IgnoreCase)).ToList();

        if (!filtered.Any())
        {
            return Enumerable.Empty<string>();
        }

        return filtered.Select(x => x.Key).ToImmutableList();
    }

    public FileInfo? GetFile(string filePath)
    {
        var availability = FileHelper.CanAccessFile(filePath, FileAccess.Read);

        if (availability != FileAccessResult.Available)
        {
            throw new FileLoadException(availability.AccessMessage(filePath, FileAccess.Read));
        }

        if (_mediaRepo.TryGetValue(filePath, out FileInfo? fileInfo))
        {
            return fileInfo;
        }
        else
        {
            throw new FileNotFoundException($"FileInfo was not found: {filePath}");
        }
    }
}