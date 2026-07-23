using System.ComponentModel.DataAnnotations;
namespace shared.data;

public class DatabaseConfiguration
{
    public const string SectionName = nameof(DatabaseConfiguration);

    public static string DJ_MEDIA_DATABASE_PATH { get; } = "DJ_MEDIA_DATABASE_PATH";

    //Not needed for all consumers
    public string DataFile
    {
        get;
        set;
    } = "data/data.db";

}
