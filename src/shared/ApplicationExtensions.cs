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
using shared.data;
using shared.http;

namespace shared;

public static partial class ApplicationExtensions
{
    public static IHostApplicationBuilder AddServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IMediaCollection, MediaCollection>()
                        .AddSingleton<shared.data.IMediaDatabase, shared.data.MediaDatabase>()
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
        return System.IO.File.ReadAllText(filePath);
    }

    public static IHostApplicationBuilder AddConfiguration(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<ThesaurusConfiguration>(builder.Configuration.GetSection(ThesaurusConfiguration.SectionName));

        builder
                .ConfigureHost()
                .ConfigureMediaCollection()
                .ConfigureMediaDatabase()
                .ConfigureJwt()
                .ConfigureTmdb();

        return builder;
    }

    public static IHostApplicationBuilder ConfigureHost(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<HostConfiguration>(builder.Configuration.GetSection(HostConfiguration.SectionName));

        var hostConfig = builder.Configuration.GetSection(HostConfiguration.SectionName).Get<HostConfiguration>();

        var combinedIp = new List<string>();

        if (hostConfig?.CorsAllowedUrl != null)
        {
            combinedIp.AddRange(hostConfig.CorsAllowedUrl);
        }

        var allowedIp = Environment.GetEnvironmentVariable(HostConfiguration.DJ_HOST_ALLOWED_CORS_URL);

        if (allowedIp is not null)
        {
            combinedIp = combinedIp.Union(allowedIp!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToList();

            var allowedIpConfigDict = new Dictionary<string, string?> { { $"{nameof(HostConfiguration)}:{nameof(HostConfiguration.CorsAllowedUrl)}", System.Text.Json.JsonSerializer.Serialize(combinedIp) } };

            builder.Configuration.AddInMemoryCollection(allowedIpConfigDict);
        }

        return builder;
    }

    public static IHostApplicationBuilder ConfigureMediaDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<shared.data.MediaDatabaseConfiguration>(builder.Configuration.GetSection(MediaDatabaseConfiguration.SectionName));

        var dbPath = Environment.GetEnvironmentVariable(MediaDatabaseConfiguration.DatabasePathKey);

        if (dbPath is not null)
        {
            var mediaConfigDict = new Dictionary<string, string?> { { "MediaCollectionConfiguration:BaseDirectory", dbPath } };

            builder.Configuration.AddInMemoryCollection(mediaConfigDict);
        }

        return builder;
    }

    public static IHostApplicationBuilder ConfigureMediaCollection(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<MediaCollectionConfiguration>(builder.Configuration.GetSection(MediaCollectionConfiguration.SectionName));

        var mediaPath = Environment.GetEnvironmentVariable(MediaCollectionConfiguration.DJ_MEDIA_BASE_DIRECTORY_KEY);

        if (mediaPath is not null)
        {
            var mediaConfigDict = new Dictionary<string, string?> { { "MediaCollectionConfiguration:BaseDirectory", mediaPath } };

            builder.Configuration.AddInMemoryCollection(mediaConfigDict);
        }

        return builder;
    }

    public static IHostApplicationBuilder ConfigureJwt(this IHostApplicationBuilder builder)
    {
        builder.Services
            .Configure<JwtConfiguration>(builder.Configuration.GetSection(HostConfiguration.SectionName).GetSection("Jwt"));

        var envSettings = new Dictionary<string, string?>
        {
            { "HostConfiguration:Jwt:Issuer", Environment.GetEnvironmentVariable(JwtConfiguration.DJ_JWT_ISSUER) },
            { "HostConfiguration:Jwt:Audience" , Environment.GetEnvironmentVariable(JwtConfiguration.DJ_JWT_AUDIENCE)},
        };

        builder.Configuration.AddInMemoryCollection(envSettings);

        return builder;
    }

    public static IHostApplicationBuilder ConfigureTmdb(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<TMDBConfiguration>(builder.Configuration.GetSection(TMDBConfiguration.SectionName));

        var tmdbDict = new Dictionary<string, string?>();

        var tmdbApiKey = TMDBConfiguration.GetApiKey();

        if (tmdbApiKey is not null)
        {
            tmdbDict.Add("TMDB:ApiKey", TMDBConfiguration.GetApiKey());
        }

        var tmdbDatabasePath = Environment.GetEnvironmentVariable(TMDBConfiguration.DatabasePathKey);

        if (tmdbDatabasePath is not null)
        {
            tmdbDict.Add("TMDB:DatabasePath", tmdbDatabasePath);
        }

        if (tmdbDict.Any())
        {
            builder.Configuration.AddInMemoryCollection(tmdbDict);
        }

        return builder;
    }

    public static IHostApplicationBuilder AddSecurity(this IHostApplicationBuilder builder)
    {

        //TODO: correlation ID for http context

        builder.Services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
                options.HttpsPort = 7132;
            });

        //must be called before security services are added
        builder.AddSecurityConfiguration();

        builder.AddOpenIddict();

        builder.Services.AddSingleton<ITokenGenerator, AnonymousTokenGenerator>();

        // we're not using anonymous tokens at this time
        // builder.AddAnonymousTokenService();

        return builder;
    }

    private static IHostApplicationBuilder AddAnonymousTokenService(this IHostApplicationBuilder builder)
    {
        var host = builder.Configuration.GetSection(HostConfiguration.SectionName).Get<HostConfiguration>();

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
                    ValidIssuer = host?.Jwt.Issuer ?? "",
                    ValidAudience = host?.Jwt.Audience ?? "",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(host?.Jwt.Key ?? "")),
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

            var healthSettings = hostSettings?.RateLimiters["health"] ?? new RateLimiterConfiguration();
            options.AddFixedWindowLimiter("health", opt =>
            {
                opt.PermitLimit = healthSettings.PermitLimit;
                opt.Window = TimeSpan.FromSeconds(healthSettings.WindowSeconds);
                opt.QueueProcessingOrder = Enum.TryParse(healthSettings.QueueProcessingOrder, out QueueProcessingOrder order) ? order : QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = healthSettings.QueueLimit;
            });


            var heavySettings = hostSettings?.RateLimiters["heavy"] ?? new RateLimiterConfiguration();
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
        var hostConfig = app.Configuration.GetSection(HostConfiguration.SectionName).Get<HostConfiguration>();

        ArgumentNullException.ThrowIfNull(hostConfig);

        var allowedUrl = hostConfig.CorsAllowedUrl.Distinct();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors(policy => policy.WithOrigins(allowedUrl.ToArray()).AllowAnyMethod().AllowAnyHeader())
                .UseHttpsRedirection()
                .UseAuthentication()
                .UseAuthorization();
        }

        app.UseRateLimiter();

        return await app.SetupTestClient();
    }
}