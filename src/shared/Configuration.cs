namespace shared;

public class MediaCollectionConfiguration
{
    public const string SectionName = nameof(MediaCollectionConfiguration);

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
