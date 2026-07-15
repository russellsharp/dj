using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace dj.test.system;

[CollectionDefinition("WebAppBase")]
public class WebAppFixture : ICollectionFixture<WebApplication> { }

public class WebApplication : ISystemFixture
{
    public WebApplicationFactory<Program> Application;
    public HttpClient Client { get; private set; }
    public string _securityEndpoint = "/api/token/anonymous";
    public string? _securityToken;

    public async Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null)
    {
        var uri = new Uri(Client.BaseAddress, endpoint);

        var uriWithParameters = parameters != null ? new Uri(QueryHelpers.AddQueryString(uri.ToString(), parameters)) : uri;

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token != null ? token : _securityToken);

        return await Client.GetAsync(uriWithParameters);
    }

    public async Task Initialize()
    {
        Application = new WebApplicationFactory<Program>();

        Client = Application.CreateClient();

        await RequestSecurityToken();
    }

    public async Task RequestSecurityToken()
    {
        var tokenResponse = await Client.GetAsync(_securityEndpoint);

        tokenResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        _securityToken = await tokenResponse.Content.ReadAsStringAsync();
    }
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
