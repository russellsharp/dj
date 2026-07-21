using System.Diagnostics;
using FluentAssertions;
using shared;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using dj.benchmarks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using Xunit.Internal;

namespace dj.test;

public class FileHelperTests(ITestOutputHelper output) : BaseTest(output)
{

    [Fact]
    public void Available()
    {
        FileAccessResult result = FileAccessResult.Available;
        var path = @"c:/media/metal/AngelOfDeath.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File is available for access: {FileAccess.Read}, " + path);
    }

    [Fact]
    public void NoAccess()
    {
        FileAccessResult result = FileAccessResult.UnauthorizedAccessException;
        var path = @"c:/media/metal/AngelOfDeath.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File cannot be accessed with requested access: {FileAccess.Read}, " + path);
    }

    [Fact]
    public void DoesNotExist()
    {
        FileAccessResult result = FileAccessResult.DoesNotExist;
        var path = @$"c:/media/metal/{Guid.NewGuid}.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File does not exist: {FileAccess.Read}, " + path);
    }


    [Fact]
    public void Locked()
    {
        FileAccessResult result = FileAccessResult.Locked;
        var path = @"c:/media/metal/AngelOfDeath.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File is locked by another process: {FileAccess.Read}, " + path);
    }

    [Fact(Skip = "Only for benchmarks."), Trait("Purpose", "Benchmark")]
    public void FileHashBenchmark()
    {
        // Act: Run the benchmarks programmatically
        var summary = BenchmarkRunner.Run<FileHelperBenchmarks>();
        MarkdownExporter.Default.ExportToLog(summary, ConsoleLogger.Default);

        summary.Reports.ForEach(x => _output.WriteLine(x.ResultStatistics.ToString()));
    }
}
