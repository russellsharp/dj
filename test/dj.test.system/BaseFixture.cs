using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;

using System.Net.Http.Json;
namespace dj.test.system;

public class BaseFixture : ISystemFixture
{
    public string _securityEndpoint = "/api/token/anonymous";
    public string? _securityToken;

    protected CancellationTokenSource _cts = new();
    public HttpClient Client { get; protected set; }

    public async Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null)
    {
        var uri = new Uri(Client.BaseAddress, endpoint);

        var uriWithParameters = parameters != null ? new Uri(QueryHelpers.AddQueryString(uri.ToString(), parameters)) : uri;

        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token != null ? token : _securityToken);

        return await Client.GetAsync(uriWithParameters);
    }

    public virtual async Task Initialize()
    {

    }

    protected async Task RequestAnonymousToken()
    {
        var tokenResponse = await Client.GetAsync(_securityEndpoint);

        tokenResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        _securityToken = await tokenResponse.Content.ReadAsStringAsync();
    }

    protected async Task RequestScopedToken()
    {
        // Define the OAuth 2.0 payload parameters
        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "console-app-client" },
            { "client_secret", "super-secret-password-123" },
            { "scope", "read:items" } // Requesting our specific scope privilege
        };

        // Send as application/x-www-form-urlencoded
        var response = await Client.PostAsync("api/token/scoped", new FormUrlEncodedContent(requestBody));

        if (response.IsSuccessStatusCode)
        {
            var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>();
            Console.WriteLine($"Access Token: {tokenData?.AccessToken}");
        }
    }
}


public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);