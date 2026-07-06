using System.Data;
using System.Diagnostics;
using System.Text.Json;
using api.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using shared;
using shared.thesaurus;
using shared.TMDB;
using shared.TMDB.Models;

namespace api.controllers;

[ApiController]
[Route("api/media")]
public class djController(
    IOptions<MediaCollectionConfiguration> _configuration,
    IMediaCollection _media,
    ITMDB _tmdb,
    ITaskMonitor _monitor,
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

    [HttpGet("match/queries")]
    public async Task<MatchQueries> MatchQueries([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] int minimumHits)
    {
        var localMedia = await _media.Files(type);

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, true); ;

        var context = new MatchingContext
        {
            MinimumScore = 100,
            PathDepthMin = 1,
            PathDepthMax = 2
        };

        var matchedMovies = new Dictionary<string, MatchScore<Result>>();

        foreach (var media in localMedia)
        {
            if (media.path is null) continue;

            var matchesByPath = await _tmdb.PathToTmdb(media.path, context, true, _cts.Token);

            if (matchesByPath is null) continue;

            var tmdbMatch = matchesByPath.FirstOrDefault();

            if (tmdbMatch is null) continue;

            var matchCount = SearchHelpers.MatchString(sanitized, tmdbMatch.title, _cts.Token) * 1;
            matchCount += SearchHelpers.MatchString(sanitized, tmdbMatch.overview, _cts.Token) * 2;

            if (matchedMovies.ContainsKey(media.path))
            {
                matchedMovies[media.path].Hits += matchCount;
            }
            else
            {
                matchedMovies.Add(media.path, new MatchScore<Result> { Hits = matchCount, Details = tmdbMatch });
            }
        }

        var orderedMatches = matchedMovies.Values.OrderByDescending(x => x.Hits);

        return new MatchQueries
        {
            Results = orderedMatches.Where(x => x.Hits >= minimumHits).ToList()
        };
    }

    [HttpGet("match/local")]
    public async Task<Matches> MatchLocal([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] int minimumHits)
    {
        var localMedia = await _media.Files(type);

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, true); ;

        var context = new MatchingContext
        {
            MinimumScore = 100,
            PathDepthMin = 1,
            PathDepthMax = 2
        };

        var matchedMovies = new Dictionary<string, MatchScore<MediaReferences>>();

        foreach (var media in localMedia)
        {
            if (media.path is null) continue;

            var matches = await _tmdb.QueryOverviews(query, minimumHits, _cts.Token);

            foreach (var tmdbMatch in matches)
            {
                if (matchedMovies.ContainsKey(media.path))
                {
                    matchedMovies[media.path].Hits += tmdbMatch.Hits;
                }
                else
                {
                    var entry = new MediaReferences(media);
                    entry.References.Add(tmdbMatch.Details);
                    matchedMovies.Add(media.path, new MatchScore<MediaReferences> { Hits = tmdbMatch.Hits, Details = entry });
                }
            }
        }

        var orderedMatches = matchedMovies.Values.OrderByDescending(x => x.Hits);

        return new Matches
        {
            Suggestions = orderedMatches.Where(x => x.Hits >= minimumHits).ToList()
        };
    }

    private static IOptions<ThesaurusConfiguration> thesaurusOptionsDefaults = Options.Create(new ThesaurusConfiguration()
    {
        DictionaryPath = "wordnet/staticdata/",
        DatabasePath = "wordnet/database/wordnet.db"
    });

    [HttpGet("match/local/synonyms")]
    public async Task<Matches> MatchLocalSynonyms([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] int minimumHIts)
    {
        var localMedia = await _media.Files(type);

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, false);

        //find matches in tmdb overviews

        List<MatchScore<MovieDetailsResponse>> queryMatches = (await _tmdb.QueryOverviews(query, minimumHIts)).ToList();

        var thesus = new Thesaurus(thesaurusOptionsDefaults);

        var searchTerms = query.Split(' ').ToList();

        var synonymTasks = searchTerms.Select(async x => (await thesus.Search(x)).ToList());

        var synonyms = (await Task.WhenAll(synonymTasks)).ToList();

        //add original terms as a group
        synonyms.Add(searchTerms);

        queryMatches.AddRange(await _tmdb.QueryWithGroupedTerms(synonyms.ToList(), minimumHIts));

        Console.WriteLine($"remote match count: {queryMatches.Count()}");
        //filter matches for local, maybe switch for not filtering

        var localMatches = new Matches() { Suggestions = new() };

        foreach (var tmdbMatch in queryMatches)
        {
            var titleTerms = tmdbMatch.Details.title.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            titleTerms.ToList().ForEach(x => Console.WriteLine(x));

            var titleMatches = await _media.FindInPath<shared.data.File>(titleTerms, titleTerms.Count(), _cts.Token);

            Console.WriteLine($" title matches count: {titleMatches.Count()}");

            //matched local to remote matches
            if (titleMatches is not null)
            {

                log($"{titleMatches.Count()}");

                titleMatches = titleMatches.OrderBy(x => x.Hits);

                foreach (var localMatch in titleMatches)
                {
                    var localAndRemoteMatch = new MediaReferences(localMatch.Details)
                    {
                        References = new List<MovieDetailsResponse> { tmdbMatch.Details }
                    };
                    var entry = new MatchScore<MediaReferences> { Hits = localMatch.Hits, Details = localAndRemoteMatch };
                    localMatches.Suggestions.Add(entry);
                }
            }
        }
        localMatches.Suggestions = localMatches.Suggestions.DistinctBy(x => x.Details.path).ToList();
        return localMatches;
    }

    private CancellationTokenSource _updateTokenSource = new();

    private static CancellationToken? _linkedToken = null;

    [HttpPost("update")]
    public async Task<MediaUpdateStatus> Update([FromQuery] bool fromScratch = false, [FromQuery] string? baseDirectory = null)
    {
        var status = _media.Status;

        if (status.InProgress)
        {
            return new MediaUpdateStatus(status, _monitor?.Status);
        }

        Console.WriteLine("kicking off");

        _updateTokenSource.TryReset();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_updateTokenSource.Token, _cts.Token);

        //do not await.
        Task updateTask = Task.Run(async () => _media.UpdateRepos(baseDirectory, fromScratch, linkedCts.Token), linkedCts.Token);
        _monitor.Set(updateTask, linkedCts);

        return new MediaUpdateStatus(status, _monitor?.Status);
    }

    [HttpGet("update/status")]
    public async Task<MediaUpdateStatus> UpdateStatus()
    {
        var status = _media.Status;
        return new MediaUpdateStatus(status, _monitor.Status);
    }

    [HttpPost("update/cancel")]
    public async Task<MediaUpdateStatus> UpdateCancel()
    {
        Console.WriteLine("Requesting cancellation.");

        _monitor.CancelRequest();

        var status = _media.Status;

        return new MediaUpdateStatus(status, _monitor?.Status);
    }
}