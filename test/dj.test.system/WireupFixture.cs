using System.Text.Json.Serialization;
using api.controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using shared;

namespace dj.test.system;

[CollectionDefinition("WireupBase")]
public class WireupFixture : ICollectionFixture<Wireup> { }

public class Wireup : BaseFixture, IDisposable
{
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
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

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

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, cts.Token);
        builder.Services.AddSingleton(linkedCts);

        builder.Services.AddSingleton<IDataManagement, DataManagement>();

        builder.WebHost.UseTestServer();
        builder.WebHost.UseSetting("https_port", "443");

        var app = builder.Build();
        app.UseRouting();
        await app.SetupSecurity(); //must come before MapControllers
        app.MapControllers();

        await app.StartAsync();

        Client = app.GetTestClient();

        await RequestAnonymousToken();

        //initialize the media service to preload the files from database
        var media = app.Services.GetRequiredService<IMediaCollection>();
        await media.Initialize(cts.Token);
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

    ~Wireup()
    {
        Dispose(false);
    }
    #endregion IDisposable
}