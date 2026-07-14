using System.Diagnostics;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using shared;
using Xunit;
using Xunit.Abstractions;

namespace dj.test.system;

[CollectionDefinition("SearchCalls")]
public class SearchFixture : ICollectionFixture<SystemFixture> { }

[Collection("SearchCalls")]
public class SearchTests(SystemFixture _fixture, ITestOutputHelper _output)
{
    [Fact]
    public async Task TestTest()
    {
        await _fixture.Initliaize();
        true.Should().BeTrue();
        Debug.WriteLine("helloh");
        Console.WriteLine("ahahhaahah");
        _output.WriteLine("hellooooooo");
    }
}

public class SystemFixture : IDisposable
{
    protected IHost _host;
    protected HttpClient _client;
    public SystemFixture()
    {
        // Initliaize().GetAwaiter().GetResult();
    }

    public async Task Initliaize()
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

        // Add services to the container.

        builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.AddConfiguration()
                .AddServices()
                .AddSecurity()
                .AddRateLimiter();

        builder.Services.AddSingleton(cts);

        var app = builder.Build();
        app.SetupSecurity(); //must come before MapControllers
        app.MapControllers();

        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer() // In-memory server
                    .Configure(app => app.Run(async ctx => await ctx.Response.WriteAsync("Hello")));
            })
            .StartAsync();


        _client = _host.GetTestClient();

        var response = await _client.GetStringAsync("/");
        response.Should().NotBeNullOrEmpty();
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

    ~SystemFixture()
    {
        Dispose(false);
    }
    #endregion IDisposable
}