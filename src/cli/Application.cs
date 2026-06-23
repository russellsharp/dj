using System.Diagnostics;
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

        await _mediaCollection.UpdateRepos(baseDirectory, false, _tokenSource.Token);

        var patterns = @".+\.avi$;.+\.mp4$;.+\.mkv$;.+\.wmv$;.+\.mpg$;".ToLowerInvariant().Split(';', StringSplitOptions.RemoveEmptyEntries);

        patterns.ToList().ForEach(x => Debug.WriteLine(x));

        try
        {
            var searchCollection = await _mediaCollection.Search(patterns, _tokenSource.Token);

            var fileMatches = searchCollection.OrderBy(x => x).ToList();

            fileMatches.ForEach(x => Debug.WriteLine(x));

            foreach (var movieFilePath in fileMatches.Take(600))
            {
                var keywords = SearchHelpers.SanitizeForSearch(movieFilePath, _tokenSource.Token, true);

                Debug.WriteLine(string.Join(" ", keywords));

                var result = await _repo.QueryTitle(string.Join(" ", keywords));

                if (result != null && result!.results.Any())
                {
                    foreach (var entry in result.results.Take(3))
                    {
                        var movie = await _repo.Movie((int)entry.id);
                        Debug.WriteLine($"Movie details found for: {movie!.title}");
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
