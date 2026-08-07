using System.Data;
using api.models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using shared;
using shared.thesaurus;
using shared.TMDB;
using shared.TMDB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace api.controllers;

[Authorize]
[ApiController]
[Route("api/media")]
public class DjController(
    IMediaCollection _media,
    ITMDB _tmdb,
    ITaskMonitor _monitor,
    ILogger<DjController> _logger,
    CancellationTokenSource _cts) : ControllerBase
{
    [HttpGet("search")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Matches))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    public async Task<ActionResult<QueryResults>> Search([FromQuery, StringLength(100)] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search terms were empty.");
        }

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (!searchTerms.Any()) return BadRequest("Search terms were empty.");

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, true);

        var searchMatches = await _media.FindInPath<shared.data.File>(sanitizedTerms, sanitizedTerms.Count(), _cts.Token);

        var results = searchMatches.OrderBy(x => x.Hits).Select(x => new Media { FilePath = x.Details.path, Title = x.Details.path, Type = MediaType.Video, Hits = Convert.ToInt32(x.Hits) });

        return Ok(new QueryResults { Media = results.ToList() });
    }

    [HttpGet("query")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(TMDBResults), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    public async Task<ActionResult<TMDBResults>> Query([FromQuery] string query, [FromQuery] MediaType type)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search terms were empty.");
        }

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (!searchTerms.Any()) return BadRequest("Search terms were empty.");

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, true);

        if (!sanitizedTerms.Any()) return BadRequest($"Search terms were empty after sanitization: Pre-sanitization: {searchTerms}.");

        var searchMatches = await _tmdb.QueryTitle(string.Join(' ', sanitizedTerms), 1, _cts.Token);

        var results = searchMatches.results
                        .OrderByDescending(x => x.popularity)
                        .Select(x => new TMDBSummary { Id = x.id.Value, Title = x.title, Type = MediaType.Video, Rank = x.popularity.Value, Overview = x.overview });

        return Ok(new TMDBResults { Media = results.ToList() });
    }

    [HttpGet("details")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(TMDBDetailResults), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    public async Task<ActionResult<TMDBDetailResults>> Details([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] bool updateRepo = false)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Empty query given.");
        }

        if (updateRepo)
        {
            _logger.LogInformation("Updating repo...");

            await _media.UpdateRepos(null, false, _cts.Token);

            _logger.LogInformation("Update complete.");
        }

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, true);

        if (!sanitizedTerms.Any())
        {
            return BadRequest($"Query empty after sanitization: {searchTerms}");
        }

        var searchMatches = await _tmdb.QueryTitle(string.Join(' ', sanitizedTerms), 1, _cts.Token);

        var detailQueries = searchMatches.results.Select(async x => await _tmdb.GetMovie(x.id.Value));

        var details = await Task.WhenAll(detailQueries);

        var results = details.Select(x => new TMDBDetails { Id = x.id, ImdbId = x.imdb_id, Overview = x.overview, Rank = x.popularity.Value, Title = x.title, Type = MediaType.Video });

        return Ok(new TMDBDetailResults { Media = results.ToList() });
    }


    [HttpGet("media")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(MediaFiles), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaFiles>> Media([FromQuery] MediaType type)
    {
        var files = await _media.Files(type);

        return Ok(new MediaFiles { Files = files.ToList() });
    }

    [HttpGet("match/queries")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(MatchQueries), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    public async Task<ActionResult<MatchQueries>> MatchQueries([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] int minimumHits)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Empty query given.");
        }

        var localMedia = await _media.Files(type);

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, true); ;

        if (!sanitized.Any())
        {
            return BadRequest($"Empty query after sanitization: {sanitized}.");
        }

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

        return Ok(new MatchQueries
        {
            Results = orderedMatches.Where(x => x.Hits >= minimumHits).ToList()
        });
    }

    [HttpGet("match/local")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(Matches), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))]
    public async Task<ActionResult<Matches>> MatchLocal([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] uint minimumHits)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query given is empty.");
        }

        var localMedia = await _media.Files(type);

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, true); ;

        if (!sanitized.Any())
        {
            return NotFound("Query empty after sanitization.");
        }

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

        return Ok(new Matches
        {
            Suggestions = orderedMatches.Where(x => x.Hits >= minimumHits).ToList()
        });
    }

    private static IOptions<ThesaurusConfiguration> thesaurusOptionsDefaults = Options.Create(new ThesaurusConfiguration()
    {
        DictionaryPath = "wordnet/staticdata/",
        DatabasePath = "wordnet/database/wordnet.db"
    });

    [HttpGet("match/local/synonyms")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(Matches), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    public async Task<ActionResult<Matches>> MatchLocalSynonyms([FromQuery] string query, [FromQuery] MediaType type, [FromQuery] uint minimumHIts)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query given is empty.");
        }

        var localMedia = await _media.Files(type);

        query = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries));

        var sanitized = SearchHelpers.SanitizeForSearch(query, _cts.Token, false);

        if (!sanitized.Any())
        {
            return BadRequest("Query empty after sanitization.");
        }

        //find matches in tmdb overviews

        List<MatchScore<MovieDetailsResponse>> queryMatches = (await _tmdb.QueryOverviews(query, minimumHIts)).ToList();

        var thesus = new Thesaurus(thesaurusOptionsDefaults, new LoggerFactory().CreateLogger<Thesaurus>());

        var searchTerms = query.Split(' ').ToList();

        var synonymTasks = searchTerms.Select(async x => (await thesus.Search(x)).ToList());

        var synonyms = (await Task.WhenAll(synonymTasks)).ToList();

        //add original terms as a group
        synonyms.Add(searchTerms);

        queryMatches.AddRange(await _tmdb.QueryWithGroupedTerms(synonyms.ToList(), minimumHIts));
        //filter matches for local, maybe switch for not filtering

        var localMatches = new Matches() { Suggestions = new() };

        foreach (var tmdbMatch in queryMatches)
        {
            var titleTerms = tmdbMatch.Details.title.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var titleMatches = await _media.FindInPath<shared.data.File>(titleTerms, titleTerms.Count(), _cts.Token);

            //matched local to remote matches
            if (titleMatches is not null)
            {

                _logger.LogInformation($"{titleMatches.Count()}");

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

        return Ok(localMatches);
    }

    private CancellationTokenSource _updateTokenSource = new();
    private static Guid MediaUpdateJobId = new Guid("f5622381-5d13-4a8d-b477-55ef23c2a1dd");
    private static Guid TmdbUpdateJobId = new Guid("04abfbfd-287a-4d2e-acc4-b54e54136ae0");

    [HttpPost("update")]
    [Authorize(Policy = "WriteScope")]
    [ProducesResponseType(typeof(MediaUpdateStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    public async Task<ActionResult<MediaUpdateStatus>> Update([FromQuery] bool fromScratch = false, [FromQuery] string? baseDirectory = null)
    {
        var status = _media.Status;

        if (status.State == UpdateState.Running)
        {
            var taskStatus = _monitor.Status(MediaUpdateJobId);
            return Ok(new MediaUpdateStatus(status, taskStatus));
        }

        _updateTokenSource.TryReset();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_updateTokenSource.Token, _cts.Token);

        //do not await.
        Task updateTask = Task.Run(async () => await _media.UpdateRepos(baseDirectory, fromScratch, linkedCts.Token), linkedCts.Token);
        _monitor.Set(MediaUpdateJobId, updateTask, linkedCts);

        status = _media.Status;

        return Ok(new MediaUpdateStatus(status, _monitor?.Status(MediaUpdateJobId)));
    }

    [HttpGet("update/status")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MediaUpdateStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaUpdateStatus>> UpdateStatus([FromQuery] int taskId)
    {
        try
        {
            var status = _media.Status;
            return Ok(new MediaUpdateStatus(status, _monitor.Status(MediaUpdateJobId)));
        }
        catch (TaskDoesNotExist)
        {
            return NotFound(new ProblemDetails { Detail = "Media update task has not been started." });
        }
    }

    [HttpPost("update/cancel")]
    [Authorize(Policy = "WriteScope")]
    [ProducesResponseType(typeof(MediaUpdateStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MediaUpdateStatus>> UpdateCancel()
    {
        try
        {
            var status = _tmdb.Status;

            if (status.State != UpdateState.Running)
            {
                return Ok(new MediaUpdateStatus(status, _monitor.Status(MediaUpdateJobId), $"Media update task is not running."));
            }

            _monitor.CancelRequest(MediaUpdateJobId);

            status = _media.Status;

            return Ok(new MediaUpdateStatus(status, _monitor?.Status(MediaUpdateJobId)));

        }
        catch (TaskDoesNotExist)
        {
            return NotFound(new ProblemDetails() { Detail = "Media update task does not exist." });
        }
    }

    [HttpPost("tmdb/update")]
    [Authorize(Policy = "WriteScope")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MediaUpdateStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaUpdateStatus>> TmdbUpdate()
    {
        var status = _tmdb.Status;

        if (status.State == UpdateState.Running)
        {
            return BadRequest(new ProblemDetails { Detail = "TMDB populate task is already running." });
        }

        _updateTokenSource.TryReset();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_updateTokenSource.Token, _cts.Token);

        var files = (await _media.Files(MediaType.Video)).ToList();

        var context = new MatchingContext
        {
            MinimumScore = 100,
            PathDepthMin = 1,
            PathDepthMax = 2
        };

        var paths = files.Select(x => x.path);

        try
        {
            var populateTask = _tmdb.Populate(paths, context, false, linkedCts.Token);

            _monitor.Set(TmdbUpdateJobId, populateTask, linkedCts);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new ProblemDetails { Detail = "TMDB populate task already in progress." });
        }

        status = _tmdb.Status;

        return Ok(new MediaUpdateStatus(status, _monitor.Status(TmdbUpdateJobId)));
    }

    [HttpPost("tmdb/update/cancel")]
    [Authorize(Policy = "WriteScope")]
    [ProducesResponseType(typeof(Matches), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MediaUpdateStatus>> TmdbUpdateCancel()
    {
        try
        {
            var status = _tmdb.Status;

            if (status.State != UpdateState.Running)
            {
                return Ok(new MediaUpdateStatus(status, _monitor.Status(TmdbUpdateJobId), $"TMDB populate task is not running."));
            }

            _monitor.CancelRequest(TmdbUpdateJobId);
            status = _tmdb.Status;
            return Ok(new MediaUpdateStatus(status, _monitor.Status(TmdbUpdateJobId)));
        }
        catch (TaskDoesNotExist)
        {
            return NotFound(new ProblemDetails { Detail = "TMDB update task does not exist." });
        }
    }

    [HttpGet("tmdb/update/status")]
    [Authorize(Policy = "ReadScope")]
    [ProducesResponseType(typeof(MediaUpdateStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaUpdateStatus>> TmdbUpdateStatus()
    {
        try
        {
            var status = _tmdb.Status;
            return Ok(new MediaUpdateStatus(status, _monitor.Status(TmdbUpdateJobId)));
        }
        catch (TaskDoesNotExist)
        {
            var status = _tmdb.Status;
            return Ok(new MediaUpdateStatus(status, null, "TMDB update task does not exist."));
        }
    }
}