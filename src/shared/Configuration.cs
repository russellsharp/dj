using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace shared;

public class Configuration
{
    public const string SectionName = nameof(Configuration);
    public MediaReaderConfiguration Reader { init; get; } = new()
    {
        BaseDirectory = string.Empty,
        Filter = string.Empty,
        DirectoryRecursionDepth = 50
    };
}

public class MediaReaderConfiguration
{
    public const string SectionName = nameof(MediaReaderConfiguration);
    public required string BaseDirectory { init; get; } = string.Empty;
    public required string Filter { init; get; } = "*.*;*.";
    public required int DirectoryRecursionDepth { init; get; } = 50;

    public override string ToString()
    {
        return $"BaseDirectory: {BaseDirectory}, Filter: {Filter}";
    }
}
