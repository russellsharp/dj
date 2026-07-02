using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using shared;
using shared.data;

namespace api.Controllers;

public record QueryRequest
{
    public List<string> Keywords { get; init; }
    public MediaType Type { get; init; }
}

public record Media
{
    public string FilePath { get; set; }
    public string Title { get; set; }
    public MediaType Type { get; set; }
    public int Hits { get; set; }
}

public record QueryResults
{
    public List<Media> Media { get; set; }
}

[ApiController]
[Route("api/media")]
public class djController(IMediaCollection _media, IOptions<MediaCollectionConfiguration> _configuration, CancellationTokenSource _cts) : ControllerBase
{
    private void log(string msg)
    {
        Debug.WriteLine(msg);
        Console.WriteLine(msg);
    }

    [HttpGet("query")]
    public async Task<QueryResults> QueryMedia([FromQuery] string query)
    {
        log("Updating repo...");

        await _media.UpdateRepos(null, false, _cts.Token);

        log("Update complete.");

        var searchTerms = string.Join(' ', query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var sanitizedTerms = SearchHelpers.SanitizeForSearch(searchTerms, _cts.Token, false);

        var searchMatches = await _media.FindInPath<shared.data.File>(sanitizedTerms, sanitizedTerms.Count(), _cts.Token);

        sanitizedTerms.ToList().ForEach(x => Console.WriteLine(x));

        log($"{searchMatches.Count()}");

        var results = searchMatches.OrderBy(x => x.Hits).Select(x => new Media { FilePath = x.Details.path, Title = x.Details.path, Type = MediaType.Video, Hits = Convert.ToInt32(x.Hits) });

        return new QueryResults { Media = results.ToList() };
    }
}
