using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
namespace shared.data;

public class MediaDatabaseConfiguration : BaseDatabaseConfiguration
{
    public new const string SectionName = nameof(MediaDatabaseConfiguration);
    public const string DatabasePathKey = "DJ_MEDIA_DATABASE_PATH";
    //Not needed for all consumers
    protected override string DefaultPath { get; } = "data/media.db";
    public override string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbFilePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(DatabasePathKey) ?? DefaultPath;
            }

            return base.DatabasePath;
        }
        set
        {
            _dbFilePath = value;
        }
    }
}
