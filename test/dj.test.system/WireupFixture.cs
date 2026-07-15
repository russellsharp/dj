using System.Diagnostics;
using System.Text.Json.Serialization;
using api.controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.ClientProtocol;
using shared;
using Xunit;

namespace dj.test.system;

public interface ISystemFixture
{
    Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null);
    Task Initialize();
    Task RequestSecurityToken();
}

[CollectionDefinition("WireupBase")]
public class WireupFixture : ICollectionFixture<Wireup> { }

public class Wireup : IDisposable, ISystemFixture
{
    protected TestServer _server;
    public HttpClient Client;
    public string _securityEndpoint = "/api/token/anonymous";
    public string? _securityToken;

    public Wireup()
    {
        Initialize().GetAwaiter().GetResult();
    }

    public async Task Initialize()
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

        builder.WebHost.UseTestServer();
        builder.WebHost.UseSetting("https_port", "443");

        var app = builder.Build();
        app.SetupSecurity(); //must come before MapControllers
        app.MapControllers();

        await app.StartAsync();

        _server = app.GetTestServer();

        Client = app.GetTestClient();

        await RequestSecurityToken();
    }

    public async Task RequestSecurityToken()
    {
        var tokenResponse = await Client.GetAsync(_securityEndpoint);

        tokenResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        _securityToken = await tokenResponse.Content.ReadAsStringAsync();
    }

    public async Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null)
    {
        var uri = new Uri(Client.BaseAddress, endpoint);

        var uriWithParameters = parameters != null ? new Uri(QueryHelpers.AddQueryString(uri.ToString(), parameters)) : uri;

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token != null ? token : _securityToken);

        return await Client.GetAsync(uriWithParameters);
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