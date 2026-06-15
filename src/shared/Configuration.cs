using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.Marshalling;
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

    private string _baseDirectory = string.Empty;

    public string BaseDirectory
    {
        get
        {
            return Path.GetFullPath(_baseDirectory);
        }
        set
        {
            _baseDirectory = value;
        }
    }

    public required string Filter { set; get; } = "*.*;*.";
    public required int DirectoryRecursionDepth { set; get; } = 50;

    public override string ToString()
    {
        return $"BaseDirectory: {BaseDirectory}, Filter: {Filter}";
    }
}
