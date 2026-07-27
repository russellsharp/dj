namespace shared;

public class MediaCollectionConfiguration
{
    public const string SectionName = nameof(MediaCollectionConfiguration);
    private string _baseDirectory = string.Empty;
    public static string DJ_MEDIA_BASE_DIRECTORY_KEY { get; } = "DJ_MEDIA_BASE_DIRECTORY";

    public string BaseDirectory
    {
        get
        {
            return string.IsNullOrEmpty(_baseDirectory) ? string.Empty : Path.GetFullPath(_baseDirectory);
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
