using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;

namespace shared;

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
    public string VideoExtensions { get; set; } = @"avi;mp4$;mkv;wmv;mpg;mkv";
    public string AudioExtensions { get; set; } = @"mp3;m4a;aac;";

    public override string ToString()
    {
        return $"BaseDirectory: {BaseDirectory}, Filter: {Filter}";
    }
}
