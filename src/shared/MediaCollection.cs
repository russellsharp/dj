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

    private ConcurrentDictionary<string, FileInfo> _mediaRepo;

    private ConcurrentDictionary<string, DirectoryInfo> _directoryRepo;

    public MediaCollection(IOptions<MediaReaderConfiguration> configuration)
    {
        _configuration = configuration.Value;
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

        if (!Directory.Exists(_configuration.BaseDirectory))
        {
            throw new ArgumentException($"Configured basedirectory does not exist: \r\n\t\t {_configuration.BaseDirectory}");
        }

        try
        {
            var tempDirectoryInfo = Directory.EnumerateDirectories(_configuration.BaseDirectory)
                .AsParallel().WithCancellation(token)
                .ToDictionary(x => x, x => new DirectoryInfo(x));
            _directoryRepo = new ConcurrentDictionary<string, DirectoryInfo>(tempDirectoryInfo);

            var tempFileInfo = tempDirectoryInfo.Keys
                .AsParallel().WithCancellation(token)
                .SelectMany(x => Directory.EnumerateFiles(x))
                .ToDictionary(x => x, x => new FileInfo(x));
            _mediaRepo = new ConcurrentDictionary<string, FileInfo>(tempFileInfo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        _mediaRepo.Values.ToList().ForEach(x => Debug.WriteLine(x));
    }

    public async Task<IEnumerable<string>> Search(string pattern, CancellationToken token)
    {
        return _mediaRepo.AsParallel().WithCancellation(token)
            .Where(x => Regex.IsMatch(x.Value.Name, pattern, RegexOptions.IgnoreCase))
            .Select(x => x.Key)
            .ToImmutableList();
    }

    public FileInfo? GetFile(string filePath)
    {
        var access = FileHelper.CanAccessFile(filePath, FileAccess.Read);

        if (access != FileAccessResult.Available)
        {
            throw new FileLoadException(FileHelper.AccessMessage(filePath, FileAccess.Read, access));
        }

        if (_mediaRepo.TryGetValue(filePath, out FileInfo? fileInfo))
        {
            return fileInfo;
        }
        else
        {
            throw new FileNotFoundException($"File was not found: {filePath}");
        }
    }
}