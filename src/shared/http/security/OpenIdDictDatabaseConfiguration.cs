using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using shared.data;

namespace shared.http.security;

public class OpenIdDictDatabaseConfiguration : BaseDatabaseConfiguration
{
    private static string PathKey { get; } = "DJ_OPENIDDICT_DATABASE_PATH";
    public new static string SectionName => "OpenIdDict";
    public new static string DefaultPath = "data/openiddict.db";
    public override string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(PathKey) ?? DefaultPath;
            }
            return base.DatabasePath;
        }
        set
        {
            _dbFilePath = value;
        }
    }
}
