using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using shared;
using shared.TMDB;
using shared.TMDB.Models;

namespace cli;

public class Application(IMediaCollection _mediaCollection, IRepo _repo, CancellationTokenSource _tokenSource)
{
    const string baseDirectory = "appData";

    public async Task RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine($"Arguments passed: {string.Join(", ", args)}");
        }


        await _mediaCollection.Initialize(_tokenSource.Token);

        await _mediaCollection.UpdateRepos(baseDirectory, _tokenSource.Token);

        var patterns = @".+\.avi$;.+\.mp4$;.+\.mkv$;.+\.wmv$;.+\.mpg$;".Split(';', StringSplitOptions.RemoveEmptyEntries);

        patterns.ToList().ForEach(x => Debug.WriteLine(x));

        try
        {
            var searchCollection = await _mediaCollection.Search(patterns, _tokenSource.Token);

            var fileMatches = searchCollection.OrderBy(x => x).ToList();

            fileMatches.ForEach(x => Debug.WriteLine(x));

            foreach (var movieFilePath in fileMatches.Take(600))
            {
                var keywords = SearchHelpers.SanitizeForSearch(movieFilePath, _tokenSource.Token, 3, true);

                Debug.WriteLine(string.Join(" ", keywords));

                var result = await _repo.Query(string.Join(" ", keywords));

                if (result != null && result!.results.Any())
                {
                    foreach (var entry in result.results.Take(3))
                    {
                        if (_repo.TryMovie((int)entry.id, out MovieDetailsResponse movie))
                        {
                            Debug.WriteLine($"Movie details found for: {movie!.title}");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        await Task.CompletedTask;
    }
}
