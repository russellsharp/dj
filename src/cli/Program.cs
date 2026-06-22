using shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using cli;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTransient<Application>();
builder.AddConfiguration()
    .AddServices();

using var host = builder.Build();

var app = host.Services.GetRequiredService<Application>();
await app.RunAsync(args);

