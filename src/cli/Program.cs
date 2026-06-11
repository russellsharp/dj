using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using cli;
using Newtonsoft.Json;
using shared;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<MediaReaderConfiguration>(builder.Configuration.GetSection(MediaReaderConfiguration.SectionName))
    .AddTransient<Application>()
     .AddSingleton<IMediaCollection, MediaCollection>();

using var host = builder.Build();

var app = host.Services.GetRequiredService<Application>();
await app.RunAsync(args);

