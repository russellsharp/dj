using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using shared.TMDB;

namespace shared.http.security;

public interface IUserDatabase { }

public class UserDatabaseConfiguration : IDatabaseConfiguration
{
    private static string DatabasePathKey { get; } = "DJ_USER_DATABASE_PATH";
    public static string SectionName { get; } = "AuthUsers";
    private string _dbFilePath = "";
    public string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(DatabasePath))
            {
                _dbFilePath = Environment.GetEnvironmentVariable(DatabasePathKey) ?? throw new ArgumentNullException();
            }
            return _dbFilePath;
        }
        set
        {
            _dbFilePath = value;
        }
    }
}

public class UserDatabase : BaseSqliteDatabase, IUserDatabase
{
    private readonly CancellationTokenSource _tokenSource;
    private readonly ILogger<UserDatabase> _logger;
    protected override string? CreateQueryResource => QueryFiles.CreateDatabase;
    protected override string? TruncateQueryResource => QueryFiles.TruncateDatabase;
    protected override Type QueryAssemblyType => typeof(Cache);
    public override string SectionName => nameof(UserDatabase);

    public UserDatabase(IOptions<UserDatabaseConfiguration> config, ILogger<UserDatabase> logger, CancellationTokenSource cts)
    {
        _logger = logger;

        ArgumentNullException.ThrowIfNull(config);

        _config = config.Value;

        _tokenSource = cts;

        Connect();

        Create();
    }

    internal static class QueryFiles
    {
        public static string CreateDatabase = @"shared.Users.sql.Users_Create.sql";

        public static string TruncateDatabase = @"shared.Users.sql.Users_Truncate.sql";
    }
}
