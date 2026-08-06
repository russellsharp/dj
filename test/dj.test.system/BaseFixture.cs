using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;

using System.Net.Http.Json;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
namespace dj.test.system;

public class BaseFixture : ISystemFixture
{
    public string _securityEndpoint = "/api/token/anonymous";
    public string? _tokenAnon;
    public CancellationTokenSource Cts { get; protected set; } = new();
    private string _tokenRead;
    private string _tokenReadWrite;
    public HttpClient Client { get; protected set; }
    public virtual IServiceProvider Services
    {
        get => throw new NotImplementedException("Services get property must be overridden with implementation");
        protected set => throw new NotImplementedException("Services set property must be overridden with implementation");
    }

    public async Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null)
    {
        token ??= _tokenRead;

        Console.WriteLine($"Token {string.IsNullOrEmpty(token)} tokenRead {string.IsNullOrEmpty(_tokenRead)}");

        var uri = new Uri(Client.BaseAddress, endpoint);

        var uriWithParameters = parameters != null ? new Uri(QueryHelpers.AddQueryString(uri.ToString(), parameters)) : uri;

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return await Client.GetAsync(uriWithParameters, Cts.Token);
    }

    public virtual async Task Initialize()
    {
        await RequestReadScopedToken();
        await RequestReadWriteScopedToken();
    }

    protected async Task RequestAnonymousToken()
    {
        var tokenResponse = await Client.GetAsync(_securityEndpoint);

        tokenResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        _tokenAnon = await tokenResponse.Content.ReadAsStringAsync(Cts.Token);
    }

    protected async Task RequestReadScopedToken()
    {
        // Define the OAuth 2.0 payload parameters
        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "console-app-client-read" },
            { "client_secret", "super-secret-password-123" },
            { "scope", "media:read" } // Requesting our specific scope privilege
        };

        // Send as application/x-www-form-urlencoded
        var response = await Client.PostAsync("api/token/scoped", new FormUrlEncodedContent(requestBody));

        response.EnsureSuccessStatusCode();

        if (response.IsSuccessStatusCode)
        {
            // var tokenString = await response.Content.ReadAsStringAsync();
            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>(Cts.Token);
            _tokenRead = tokenData.access_token;
        }
    }

    protected async Task RequestReadWriteScopedToken()
    {
        // Define the OAuth 2.0 payload parameters
        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "console-app-client-rw" },
            { "client_secret", "super-secret-password-123" },
            { "scope", "media:read media:write" } // Requesting rw token
        };

        // Send as application/x-www-form-urlencoded
        var response = await Client.PostAsync("api/token/scoped", new FormUrlEncodedContent(requestBody));

        response.EnsureSuccessStatusCode();

        if (response.IsSuccessStatusCode)
        {
            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>(Cts.Token);
            _tokenReadWrite = tokenData.access_token;
        }
    }
}


public record TokenResponse(string access_token, string token_type, int expires_in);
