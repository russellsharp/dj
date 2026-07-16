using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;

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

    protected async Task RequestSecurityToken()
    {
        var tokenResponse = await Client.GetAsync(_securityEndpoint);

        tokenResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        _securityToken = await tokenResponse.Content.ReadAsStringAsync();
    }
}