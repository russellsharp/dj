using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using shared.thesaurus;
using shared.TMDB;

namespace shared;

public static class ApplicationExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IMediaCollection, MediaCollection>()
                        .AddSingleton<shared.data.IDatabase, shared.data.Database>()
                        .AddSingleton<shared.TMDB.ICache, shared.TMDB.Cache>()
                        .AddSingleton<shared.TMDB.IRepo, shared.TMDB.Repo>()
                        .AddSingleton<ITMDB, shared.TMDB.TMDB>()
                        .AddSingleton<CancellationTokenSource>()
                        .AddSingleton<shared.thesaurus.IThesaurus, shared.thesaurus.Thesaurus>();
    }

    public static IServiceCollection AddConfiguration(this IHostApplicationBuilder builder)
    {
        return builder.Services.Configure<MediaReaderConfiguration>(builder.Configuration.GetSection(MediaReaderConfiguration.SectionName))
                                .Configure<shared.data.DatabaseConfiguration>(builder.Configuration.GetSection(shared.data.DatabaseConfiguration.SectionName))
                                .Configure<shared.EndpointConfig>(builder.Configuration.GetSection("TMDB"))
                                .Configure<ThesaurusConfiguration>(builder.Configuration.GetSection("Thesaurus"));
    }
}

