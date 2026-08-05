namespace shared;

public interface IDatabaseConfiguration
{
    public static string SectionName { get; }

    private static string DatabasePathKey { get; }
    public string DatabasePath { get; set; }
}


