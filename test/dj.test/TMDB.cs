using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared.TMDB;

namespace dj.test;

public class TMDB(ITestOutputHelper _output)
{
    private static IOptions<ClientConfig> BasicOptions = Options.Create(new ClientConfig
    {
        BaseUrl = "https://api.themoviedb.org/3",
        ApiKey = Client.SUPER_SECRET_API_KEY,
    });

    public void log(string msg)
    {
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
        _output.WriteLine(msg);
    }

    [Fact]
    public async Task QueryMovies()
    {
        using Client client = new(BasicOptions);

        var movies = await client.QueryMovie("Star Wars", 1);

        movies.Should().NotBeNull();

        movies.results.Count().Should().BeGreaterThan(0);

        movies.results.ForEach(x => log(x.title));
        var firstMovie = movies.results[0];

        firstMovie.title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task QueryMovieGenres()
    {
        using Client client = new(BasicOptions);

        var genres = await client.MovieGenres();

        genres.Should().NotBeNull();

        genres.Genres.ForEach(x => log(x.Name));

        genres.Genres.Should().NotBeNullOrEmpty();

        genres.Genres.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MovieDetails()
    {
        using Client client = new(BasicOptions);

        var details = await client.Movie(11);

        details.Should().NotBeNull();

        details.genres.Should().NotBeNull();

        details.genres.Count().Should().BeGreaterThan(0);

        details.id.Should().Be(11);

        details.title.Should().Be("Star Wars");
    }
}
