using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using shared;
using System.ComponentModel;

namespace shared;

public static class ApplicationExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IMediaCollection, MediaCollection>()
                        .AddSingleton<shared.data.IDatabase, shared.data.Database>();
    }

    public static IServiceCollection AddConfiguration(this IHostApplicationBuilder builder)
    {
        return builder.Services.Configure<MediaReaderConfiguration>(builder.Configuration.GetSection(MediaReaderConfiguration.SectionName))
                                .Configure<shared.data.DatabaseConfiguration>(builder.Configuration.GetSection(shared.data.DatabaseConfiguration.SectionName));
    }
}

