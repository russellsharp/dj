using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using shared.utility;

namespace shared.http.security;

public enum Scopes
{
    [Description("media:read")]
    MediaRead,
    [Description("media:write")]
    MediaWrite
}

public static class ScopesExtensions
{
    public static string ToOidc(this Scopes value)
    {
        return value.ToDescription();
    }

    public static IHostApplicationBuilder AddScopeJsonConverters(this IHostApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new ScopeListConverter());
        });
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new ScopeListConverter());
        });
        builder.Services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new ScopeListConverter());
        });

        return builder;
    }
}

public static partial class ApplicationExtensions
{
    public static IHostApplicationBuilder AddOpenIddict(this IHostApplicationBuilder builder)
    {

        builder.AddScopeJsonConverters();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ReadScope", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c =>
                        c.Type == "scope" &&
                        c.Value.Split(' ').Contains(Scopes.MediaRead.ToOidc())
                    )
                ));

            options.AddPolicy("WriteScope", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c =>
                        c.Type == "scope" &&
                        c.Value.Split(' ').Contains(Scopes.MediaWrite.ToOidc())
                    )
                ));
        });

        builder.AddOpenIddictService();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenIddictService(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<OpenIdDictDatabaseContext>(options =>
        {
            var dbConfig = builder.Configuration.GetSection(OpenIdDictDatabaseConfiguration.SectionName).Get<OpenIdDictDatabaseConfiguration>() ?? new OpenIdDictDatabaseConfiguration();

            var dbPath = PathUtilities.GetDirectory(dbConfig.DatabasePath);

            Directory.CreateDirectory(dbPath);

            ArgumentException.ThrowIfNullOrEmpty(dbConfig?.ConnectionString);

            options.UseSqlite(dbConfig?.ConnectionString);

            options.UseOpenIddict();
        });

        builder.AddTestUserDatabase();

        builder.Services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                        .UseDbContext<TestUserDbContext>()
                        .UseDbContext<OpenIdDictDatabaseContext>()
                        ;
                })
                .AddServer(options =>
                {
                    // Register the custom scopes so OpenIddict accepts them in token requests.
                    options.RegisterScopes(["media:read", "media:write"]);

                    // Disable builtin scope validation so our controller can accept and validate custom scopes.
                    options.DisableScopeValidation();

                    // Enable the token endpoint (where clients request tokens)
                    options.SetTokenEndpointUris("/api/token/scoped");
                    options.AllowClientCredentialsFlow(); // Machine-to-machine exchange

                    // ADD THIS LINE to downgrade encryption to visible JWT format
                    // options.DisableAccessTokenEncryption();

                    SecurityConfiguration? securityConfig = new();

                    using (var temporaryServiceProvider = builder.Services.BuildServiceProvider())
                    {
                        securityConfig = temporaryServiceProvider.GetRequiredService<IOptions<SecurityConfiguration>>()?.Value;
                    }

                    Console.WriteLine($"Security Token is empty or null: {string.IsNullOrEmpty(securityConfig.SecurityKey)}");

                    // Register the cryptographic signing keys
                    if (securityConfig != null && !string.IsNullOrEmpty(securityConfig.SecurityKey))
                    {
                        try
                        {
                            var symmetricKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityConfig.SecurityKey));
                            options.AddSigningKey(symmetricKey);
                            options.AddEncryptionKey(symmetricKey);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Failed to initialize signing/encryption key.", ex);
                        }
                    }

                    if (builder.Environment.IsDevelopment())
                    {
                        // TODO: Find a use case for development keys or remove it
                        // options.AddDevelopmentEncryptionCertificate()
                        //     .AddDevelopmentSigningCertificate();

                        // No explicit key configured and is a Development environment — use ephemeral keys so
                        // the server can still issue tokens (e.g. test environments without a provisioned secret).
                        options.AddEphemeralEncryptionKey();
                        options.AddEphemeralSigningKey();
                    }

                    // Register the ASP.NET Core host
                    options.UseAspNetCore().EnableTokenEndpointPassthrough();

                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
        return builder;
    }


    public static WebApplication SetupOpenIdDictDatabase(this WebApplication app)
    {
        // Create a scope to resolve scoped dependencies safely
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                // Resolve your shared DbContext
                var context = services.GetRequiredService<OpenIdDictDatabaseContext>();

                // This will automatically run any pending migrations and create the file
                // context.Database.Migrate();

                //use Migrate or EnsureCreated but not both
                context.Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<OpenIdDictDatabaseContext>>();
                logger.LogError(ex, "An error occurred while migrating the OpenIddict database.");

                throw;
            }
        }
        return app;
    }

    public static async Task<WebApplication> SetupTestClient(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var userContext = scope.ServiceProvider.GetRequiredService<TestUserDbContext>();

            userContext.Database.EnsureCreated();

            app.SetupOpenIdDictDatabase();

            app.SetupTestData();

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            foreach (var user in userContext.UserInfo)
            {
                // Check if our test client already exists
                var application = await manager.FindByClientIdAsync(user.client_id);

                if (application is null)
                {
                    var applicationDescriptor = new OpenIddictApplicationDescriptor
                    {
                        ClientId = user.client_id,
                        ClientSecret = user.password_plaintext,
                        DisplayName = user.display_name,
                        Permissions =
                                    {                                        
                                        // Must explicitly permit the flow and endpoint
                                        OpenIddictConstants.Permissions.Endpoints.Token,
                                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials
                                    }
                    };

                    var result = await manager.CreateAsync(applicationDescriptor);

                    application = await manager.FindByClientIdAsync(user.client_id) ?? throw new Exception("Application not found.");

                    await manager.PopulateAsync(applicationDescriptor, application);

                    applicationDescriptor.AddScopePermissions([.. user.scopes.Select(x => x.ToOidc())]);

                    await manager.UpdateAsync(application, applicationDescriptor);
                }
            }
        }

        return app;
    }
}
