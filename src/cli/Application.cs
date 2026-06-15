using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using shared;

namespace cli;

public class Application(IMediaCollection _mediaCollection)
{
    const string baseDirectory = "appData";

    public async Task RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine($"Arguments passed: {string.Join(", ", args)}");
        }

        CancellationTokenSource source = new();

        await _mediaCollection.Initialize(source.Token);

        await _mediaCollection.UpdateRepos(baseDirectory, source.Token);

        var patterns = @".+\.avi$".Split(';');

        try
        {
            var tasks = patterns.Select(x => _mediaCollection.Search(x, source.Token));

            var searchCollection = await Task.WhenAll(tasks);

            var fileMatches = searchCollection.SelectMany(x => x).OrderBy(x => x).ToList();

            fileMatches.ForEach(x => Debug.WriteLine(x));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        await Task.CompletedTask;
    }
}
