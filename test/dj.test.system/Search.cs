using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace dj.test.system;

[Collection("WebAppBase")]
public class SearchTests : BaseTest
{
    private ISystemFixture _fixture;
    public SearchTests(WebApplication fixture, ITestOutputHelper logger) : base(logger)
    {
        _fixture = fixture;

        _fixture.Initialize().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Ok200()
    {
        Dictionary<string, string> searchTerms = new()
        {
            ["query"] = "training,day"
        };

        var response = await _fixture.Get($"/api/media/query", searchTerms);

        response.Should().NotBeNull();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();

        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Unauthorized401()
    {
        Dictionary<string, string> searchTerms = new()
        {
            ["query"] = "training,day"
        };

        var response = await _fixture.Get($"/api/media/query", searchTerms, "unauth");

        response.Should().NotBeNull();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();

        content.Should().BeEmpty();
    }
}