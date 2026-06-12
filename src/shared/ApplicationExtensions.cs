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
        return services.AddSingleton<IMediaCollection, MediaCollection>();
    }

    public static IServiceCollection AddConfiguration(this IHostApplicationBuilder builder)
    {
        return builder.Services.Configure<MediaReaderConfiguration>(builder.Configuration.GetSection(MediaReaderConfiguration.SectionName));
    }
}

