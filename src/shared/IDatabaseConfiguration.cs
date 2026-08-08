namespace shared;

public interface IDatabaseConfiguration
{
    public static string SectionName { get; }

    protected static string DatabasePathKey { get; }
    public string DatabasePath { get; set; }
    public string ConnectionString { get; }
}


