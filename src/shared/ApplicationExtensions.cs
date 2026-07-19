using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using shared.thesaurus;
using shared.TMDB;
using shared.http.security;

namespace shared;

public static class ApplicationExtensions
{
    public static IHostApplicationBuilder AddServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IMediaCollection, MediaCollection>()
                        .AddSingleton<shared.data.IDatabase, shared.data.Database>()
                        .AddSingleton<shared.TMDB.ICache, shared.TMDB.Cache>()
                        .AddSingleton<shared.TMDB.IRepo, shared.TMDB.Repo>()
                        .AddSingleton<ITMDB, shared.TMDB.TMDB>()
                        .AddSingleton<shared.thesaurus.IThesaurus, shared.thesaurus.Thesaurus>()
                        .AddSingleton<ITaskMonitor, TaskMonitor>();

        return builder;
    }


    private static string GetKeyFromFile()
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
        var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
        var filePath = Path.Combine(rootDir, "super_secret_key.secret");
        return File.ReadAllText(filePath);
    }

    public static IHostApplicationBuilder AddConfiguration(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<MediaCollectionConfiguration>(builder.Configuration.GetSection(MediaCollectionConfiguration.SectionName))
                                .Configure<shared.data.DatabaseConfiguration>(builder.Configuration.GetSection(shared.data.DatabaseConfiguration.SectionName))
                                .Configure<shared.EndpointConfig>(builder.Configuration.GetSection("TMDB"))
                                .Configure<ThesaurusConfiguration>(builder.Configuration.GetSection("Thesaurus"))
                                .Configure<HostConfiguration>(builder.Configuration.GetSection(HostConfiguration.SectionName))
                                .Configure<JwtConfiguration>(builder.Configuration.GetSection(HostConfiguration.SectionName).GetSection("Jwt"));
        return builder;
    }

    public static IHostApplicationBuilder AddSecurity(this IHostApplicationBuilder builder)
    {

        //TODO: correlation ID for http context

        builder.Services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
                options.HttpsPort = 7123;
            });

        //must be called before security services are added
        builder.AddSecurityConfiguration();

        builder.AddOpenIddict();

        builder.Services.AddSingleton<ITokenGenerator, AnonymousTokenGenerator>();

        // builder.AddAnonymousTokenService();

        return builder;
    }

    private static IHostApplicationBuilder AddAnonymousTokenService(this IHostApplicationBuilder builder)
    {
        var host = builder.Configuration.GetSection($"{HostConfiguration.SectionName}").Get<HostConfiguration>();

        builder.Services.AddScoped<ITokenGenerator, AnonymousTokenGenerator>()
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = host.Jwt.Issuer,
                    ValidAudience = host.Jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(host.Jwt.Key)),
                };
            });
        return builder;
    }

    public static IHostApplicationBuilder AddRateLimiter(this IHostApplicationBuilder builder)
    {

        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true,
                        PermitLimit = 100,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }
                )
            );

            var hostSettings = builder.Configuration.GetSection(HostConfiguration.SectionName).Get<HostConfiguration>();

            var healthSettings = hostSettings?.RateLimiters["health"] ?? new RateLimiterTypeConfiguration();
            options.AddFixedWindowLimiter("health", opt =>
            {
                opt.PermitLimit = healthSettings.PermitLimit;
                opt.Window = TimeSpan.FromSeconds(healthSettings.WindowSeconds);
                opt.QueueProcessingOrder = Enum.TryParse(healthSettings.QueueProcessingOrder, out QueueProcessingOrder order) ? order : QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = healthSettings.QueueLimit;
            });


            var heavySettings = hostSettings?.RateLimiters["heavy"] ?? new RateLimiterTypeConfiguration();
            options.AddSlidingWindowLimiter("heavy", opt =>
            {
                opt.PermitLimit = heavySettings.PermitLimit;
                opt.Window = TimeSpan.FromSeconds(heavySettings.WindowSeconds);
                opt.QueueProcessingOrder = Enum.TryParse(heavySettings.QueueProcessingOrder, out QueueProcessingOrder order) ? order : QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = heavySettings.QueueLimit;
            });
        });
        return builder;
    }

    public static async Task<WebApplication> SetupSecurity(this WebApplication app)
    {
        app
            .UseCors(policy => policy.WithOrigins(["https://127.0.0.1"]).AllowAnyMethod().AllowAnyHeader())
            .UseHttpsRedirection()
            .UseRateLimiter()
            .UseAuthentication()
            .UseAuthorization();


        await app.SetupTestClient();

        return app;
    }
}


public class HostConfiguration
{
    public static string SectionName = typeof(HostConfiguration).Name;
    public Dictionary<string, RateLimiterTypeConfiguration> RateLimiters { get; init; } = new();
    public JwtConfiguration Jwt { get; init; } = new();
}

public class JwtConfiguration
{
    public static string SectionName = "Jwt";
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
}

public class RateLimiterTypeConfiguration
{
    public int PermitLimit { get; init; } = 1;
    public int WindowSeconds { get; init; } = 5;
    public string QueueProcessingOrder { get; init; } = "OldestFirst";
    public int QueueLimit { get; init; } = 1;
}

