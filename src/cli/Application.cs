using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using shared;

namespace cli;

public class Application(IMediaCollection _mediaCollection)
{

    public async Task RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine($"Arguments passed: {string.Join(", ", args)}");
        }
        CancellationTokenSource source = new();

        await _mediaCollection.Populate(source.Token);

        await _mediaCollection.Search("*.avi", source.Token);

        await Task.CompletedTask;
    }
}