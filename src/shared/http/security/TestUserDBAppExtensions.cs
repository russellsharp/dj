using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using shared.util;

namespace shared.http.security;

public static class TestUserDBAppExtensions
{

    public static IHostApplicationBuilder AddTestUserDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IUserDatabase, TestUserDatabase>();

        var dbConfig = builder.Configuration.GetSection(TestUserDatabaseConfiguration.SectionName).Get<TestUserDatabaseConfiguration>() ?? new TestUserDatabaseConfiguration();

        ArgumentException.ThrowIfNullOrEmpty(dbConfig?.ConnectionString);

        var dbPath = PathUtilities.GetDirectory(dbConfig.DatabasePath);

        Directory.CreateDirectory(dbPath);

        builder.Services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(dbConfig.ConnectionString));

        return builder;
    }

    public static WebApplication SetupTestData(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var userContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();

            userContext.Database.EnsureCreated();

            // userContext.Database.Migrate();

            //seed userContext with test users
            if (!userContext.UserInfo.Any())
            {
                userContext.UserInfo.Add(new UserInformation
                {
                    client_id = "console-app-client-read",
                    scopes = [Scopes.MediaRead],
                    display_name = "console-app-client-read",
                    password_plaintext = "super-secret-password-123"
                });

                userContext.UserInfo.Add(new UserInformation
                {
                    client_id = "console-app-client-rw",
                    scopes = [Scopes.MediaWrite, Scopes.MediaRead],
                    display_name = "console-app-client-rw",
                    password_plaintext = "super-secret-password-123"
                });

            }

            if (!userContext.ApplicationScopes.Any())
            {
                userContext.ApplicationScopes.AddRange(
                    [
                        new ScopeEntry { Value = Scopes.MediaRead },
                        new ScopeEntry { Value = Scopes.MediaWrite }
                    ]);
            }

            userContext.SaveChanges();
        }
        return app;
    }
}
