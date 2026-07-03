using System.Diagnostics;
using api.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using shared;
using shared.TMDB;

namespace api.controllers;

[ApiController]
[Route("api/media")]
public class djController(
    IOptions<MediaCollectionConfiguration> _configuration,
    IMediaCollection _media,
    ITMDB _tmdb,
    CancellationTokenSource _cts) : ControllerBase
{
    private void log(string msg)
    {
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
    }

    [HttpGet("search")]
    public async Task<QueryResults> Search([FromQuery] string query)
    {
        log("Updating repo...");

        await _media.UpdateRepos(null, false, _cts.Token);

        log("Update complete.");

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, true);

        var searchMatches = await _media.FindInPath<shared.data.File>(sanitizedTerms, sanitizedTerms.Count(), _cts.Token);

        sanitizedTerms.ToList().ForEach(x => Console.WriteLine(x));

        log($"{searchMatches.Count()}");

        var results = searchMatches.OrderBy(x => x.Hits).Select(x => new Media { FilePath = x.Details.path, Title = x.Details.path, Type = MediaType.Video, Hits = Convert.ToInt32(x.Hits) });

        return new QueryResults { Media = results.ToList() };
    }

    [HttpGet("query")]
    public async Task<TMDBResults> Query([FromQuery] string query, [FromQuery] MediaType type)
    {
        log("Updating repo...");

        await _media.UpdateRepos(null, false, _cts.Token);

        log("Update complete.");

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, true);

        var searchMatches = await _tmdb.QueryTitle(string.Join(' ', sanitizedTerms), 1, _cts.Token);

        sanitizedTerms.ToList().ForEach(x => Console.WriteLine(x));

        log($"{searchMatches.results.Count()}");

        var results = searchMatches.results
                        .OrderByDescending(x => x.popularity)
                        .Select(x => new TMDBSummary { Id = x.id.Value, Title = x.title, Type = MediaType.Video, Rank = x.popularity.Value, Overview = x.overview });

        return new TMDBResults { Media = results.ToList() };
    }

    [HttpGet("details")]
    public async Task<TMDBDetailResults> Details([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] bool updateRepo = false)
    {
        if (updateRepo)
        {
            log("Updating repo...");

            await _media.UpdateRepos(null, false, _cts.Token);

            log("Update complete.");
        }

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, true);

        var searchMatches = await _tmdb.QueryTitle(string.Join(' ', sanitizedTerms), 1, _cts.Token);

        sanitizedTerms.ToList().ForEach(x => Console.WriteLine(x));

        log($"{searchMatches.results.Count()}");

        var detailQueries = searchMatches.results.Select(async x => await _tmdb.GetMovie(x.id.Value));

        var details = await Task.WhenAll(detailQueries);

        var results = details.Select(x => new TMDBDetails { Id = x.id, ImdbId = x.imdb_id, Overview = x.overview, Rank = x.popularity.Value, Title = x.title, Type = MediaType.Video });

        return new TMDBDetailResults { Media = results.ToList() };
    }


    [HttpGet("media")]
    public async Task<MediaFiles> Media([FromQuery] MediaType type, [FromQuery] bool updateRepo = false)
    {
        if (updateRepo)
        {
            log("Updating repo...");

            await _media.UpdateRepos(null, false, _cts.Token);

            log("Update complete.");
        }
        var files = await _media.Files(type);

        files.ToList().ForEach(x => Console.WriteLine(x.path));

        return new MediaFiles { Files = files.ToList() };
    }

    [HttpGet("match")]
    public async Task<List<MatchScore<shared.data.File>>> Match([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] int minimumHits)
    {
        await _media.Initialize(_cts.Token);

        var localMedia = await _media.Files(type);

        Console.WriteLine($"Local media count: {localMedia.Count()}");

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, true); ;

        Console.WriteLine(string.Join(',', sanitized));

        var context = new MatchingContext
        {
            MinimumScore = 100,
            PathDepthMin = 1,
            PathDepthMax = 2
        };

        var matchedMovies = new Dictionary<string, MatchScore<shared.data.File>>();

        foreach (var media in localMedia)
        {
            if (media.path is null) continue;

            Console.WriteLine($"media: {media.path}");

            var matchesByPath = await _tmdb.PathToTmdb(media.path, context, true, _cts.Token);

            Console.WriteLine(matchesByPath.Count());

            if (matchesByPath is null) continue;

            Console.WriteLine(1);
            foreach (var tmdbMatch in matchesByPath)
            {
                var matchCount = SearchHelpers.MatchString(sanitized, tmdbMatch.title, _cts.Token) * 1;
                matchCount += SearchHelpers.MatchString(sanitized, tmdbMatch.overview, _cts.Token) * 2;

                if (matchedMovies.ContainsKey(media.path))
                {
                    Console.WriteLine(2);
                    matchedMovies[media.path].Hits += matchCount;
                }
                else
                {
                    Console.WriteLine(3);
                    matchedMovies.Add(media.path, new MatchScore<shared.data.File> { Hits = matchCount, Details = media });
                }
            }
        }

        var orderedMatches = matchedMovies.Values.OrderByDescending(x => x.Hits);

        return orderedMatches.Where(x => x.Hits >= minimumHits).ToList();
    }
}