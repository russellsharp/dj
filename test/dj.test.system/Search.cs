using api.models;
using FluentAssertions;

namespace dj.test.system;

[Collection("WireupCollection")]
public class SearchTests : BaseTest
{
    private ISystemFixture _fixture;

    public SearchTests(WireupFixture fixture, ITestOutputHelper logger) : base(logger)
    {
        _fixture = fixture;

        Initialize().GetAwaiter().GetResult();
    }

    private async Task Initialize()
    {
        Log($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");

        _fixture.Initialize().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Ok200()
    {
        Dictionary<string, string> searchTerms = new()
        {
            ["query"] = "training,day"
        };

        var response = await _fixture.Get($"/api/media/search", searchTerms);

        response.Should().NotBeNull();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();

        content.Should().NotBeNullOrEmpty();

        var matches = System.Text.Json.JsonSerializer.Deserialize<QueryResults>(content);

        Log(content);
    }

    [Fact]
    public async Task Unauthorized401()
    {
        Dictionary<string, string> searchTerms = new()
        {
            ["query"] = "training,day"
        };

        var response = await _fixture.Get($"/api/media/search", searchTerms, "unauth");

        response.Should().NotBeNull();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync(_fixture.Cts.Token);

        content.Should().BeEmpty();
    }

    [Fact]
    public async Task Forbidden403()
    {
        Dictionary<string, string> searchTerms = new()
        {
            ["query"] = "training,day"
        };

        var response = await _fixture.Get($"/api/media/search", searchTerms, "unauth");

        response.Should().NotBeNull();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();

        content.Should().BeEmpty();
    }
}