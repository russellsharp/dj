using System.Text.Json.Serialization;
using api.controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using shared;

namespace dj.test.system;

[CollectionDefinition("WireupCollection")]
public class WireupCollection : ICollectionFixture<WireupFixture> { }

public class WireupFixture : BaseFixture, IDisposable
{
    public override IServiceProvider Services { get; protected set; }

    public override async Task Initialize()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("\nStopping program...");
            e.Cancel = true; // Prevents the app from closing immediately
            cts.Cancel();    // Sends the cancellation signal
        };

        var args = Array.Empty<string>();
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(DjController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.AddConfiguration()
                .AddServices()
                .AddSecurity()
                .AddRateLimiter();

        Cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, cts.Token);
        builder.Services.AddSingleton(Cts);

        builder.Services.AddSingleton<IDataManagement, DataManagement>();

        builder.WebHost.UseTestServer();
        builder.WebHost.UseSetting("https_port", "443");

        var app = builder.Build();
        app.UseRouting();
        await app.SetupSecurity(); //must come before MapControllers
        app.MapControllers();

        var logger = app.Services.GetRequiredService<ILogger<WireupFixture>>();
        logger.LogWarning($"Environment: {app.Environment.EnvironmentName}");
        await app.StartAsync();

        //initialize the media service to preload the files from database
        var media = app.Services.GetRequiredService<IMediaCollection>();
        await media.Initialize(Cts.Token);

        // Test widgets from here
        Client = app.GetTestClient();

        Services = app.Services;

        Client.BaseAddress = new Uri("https://localhost");

        Cts = app.Services.GetRequiredService<CancellationTokenSource>();

        //run this before using the api to grab auth tokens
        await base.Initialize();
    }

    #region IDisposable
    private int _disposed = 0;
    public void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~WireupFixture()
    {
        Dispose(false);
    }
    #endregion IDisposable
}