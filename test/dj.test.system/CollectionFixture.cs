using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using api.controllers;
using dj.test.system;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using shared;
using shared.http;
using shared.TMDB;
using Xunit;
using Xunit.Abstractions;

namespace dj.test.system;

[CollectionDefinition("SearchCalls")]
public class SearchFixture : ICollectionFixture<SystemFixture> { }

[Collection("SearchCalls")]
public class SearchTests : BaseTest
{
    private SystemFixture _fixture;
    public SearchTests(SystemFixture fixture, ITestOutputHelper logger) : base(logger)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestTest()
    {
        await _fixture.Initliaize();

        var searchTerms = "training,day";

        var response = await _fixture.Client.GetAsync($"/api/media/query?{searchTerms}");

        // var response = await _fixture.Client.GetAsync("/test");

        var content = await response.Content.ReadAsStringAsync();

        Log(content);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        content.Should().NotBeNullOrEmpty();

    }
}

public class BaseTest(ITestOutputHelper _log)
{
    public const string BaseUrl = "https://localhost:7123/api";

    public void Log(object msg)
    {
        var message = Convert.ToString(msg);
        Debug.WriteLine(message);
        Console.WriteLine(message);
        _log.WriteLine(message);
    }
}

public class SystemFixture : IDisposable
{
    protected TestServer _server;
    public HttpClient Client;
    public SystemFixture(/*TestWebApplicationFactory<Program> factory*/)
    {
        // Client = factory.CreateClient();
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

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(djController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.AddConfiguration()
                .AddServices()
                .AddSecurity()
                .AddRateLimiter();

        builder.Services.AddSingleton(cts);

        builder.WebHost.UseTestServer();
        builder.WebHost.UseSetting("https_port", "443");

        var app = builder.Build();
        app.SetupSecurity(); //must come before MapControllers
        app.MapControllers();

        await app.StartAsync();

        _server = app.GetTestServer();

        Client = app.GetTestClient();
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

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
        });

        builder.ConfigureTestServices(services =>
        {

        });
    }
}