using System.ComponentModel.DataAnnotations;
namespace shared.data;

public class DatabaseConfiguration
{
    public const string SectionName = nameof(DatabaseConfiguration);

    //Not needed for all consumers
    public string DataFile
    {
        get;
        set;
    } = "data/data.db";

}
