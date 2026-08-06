using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using shared.TMDB;

namespace shared.http.security;

public interface IUserDatabase { }

public class TestUserDatabase : BaseSqliteDatabase, IUserDatabase
{
    private readonly CancellationTokenSource _tokenSource;
    private readonly ILogger<TestUserDatabase> _logger;
    private readonly DbContextOptions<TestUserDbContext> _efContext;

    protected override string? CreateQueryResource => QueryFiles.CreateDatabase;
    protected override string? TruncateQueryResource => QueryFiles.TruncateDatabase;
    protected override Type QueryAssemblyType => typeof(Cache);
    public override string SectionName => nameof(TestUserDatabase);

    public TestUserDatabase(IOptions<TestUserDatabaseConfiguration> config, ILogger<TestUserDatabase> logger, DbContextOptions<TestUserDbContext> efContext, CancellationTokenSource cts)
    {
        _logger = logger;

        _efContext = efContext;

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
