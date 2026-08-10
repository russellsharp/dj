using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using shared.utility;
using Xunit.Internal;

namespace dj.benchmarks;

[MemoryDiagnoser]
public class FileHelperBenchmarks
{
    private static readonly CancellationTokenSource source = new();
    private static string filePath = @"testdata\hashtest.bin";

    private Random _rng = new();

    private CancellationTokenSource _tokenSource = new();

    [GlobalSetup]
    public void CreateTestFile(long sizeKilobytes = 1024)
    {
        long numberOfBytes = 1024 * sizeKilobytes;

        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        var random = new Random();
        fs.SetLength(numberOfBytes);
        long bytesWritten = 0;
        byte[] buffer = new byte[8192]; // Write in efficient 8KB chunks
        while (bytesWritten < numberOfBytes)
        {
            random.NextBytes(buffer);
            int bytesToWrite = (int)Math.Min(buffer.Length, numberOfBytes - bytesWritten);
            fs.Write(buffer, 0, bytesToWrite);
            bytesWritten += bytesToWrite;
        }
    }

    [GlobalCleanup]
    public void DeleteTestFile()
    {
        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception while cleaning up test file: {ex}");
        }
    }

    [Benchmark]
    public async Task HashSpanBuffer()
    {
        string hash = await FileHashes.HashFs(filePath);
    }

    [Benchmark]
    public async Task HashOpenRead()
    {
        string hash = await FileHashes.HashOpenRead(filePath);
    }

    [Benchmark]
    public async Task CreateFileSetForeach()
    {
        //should be cleaned up by BenchmarkDotNet
        var testFiles = Enumerable.Range(1, 300).Select(x => $"test_file_{x}");
        testFiles.ForEach(async x => await FileHelper.CreateFile(x, _rng.Next(5000), (byte)'w'));

        var testConversion = testFiles
            .AsParallel().WithCancellation(_tokenSource.Token)
            .Select(async x => await FileHelper.PathToFile(x)).ToList();
        await Task.WhenAll(testConversion);
    }

    [Benchmark]

    public async Task CreateFileSetForEachParallel()
    {
        //should be cleaned up by BenchmarkDotNet
        var testFiles = Enumerable.Range(1, 300).Select(x => $"testdata/test_file_{x}.avi");
        Parallel.ForEach(testFiles, async x => await FileHelper.CreateFile(x, _rng.Next(5000), (byte)'w'));

        var testConversion = testFiles
            .AsParallel().WithCancellation(_tokenSource.Token)
            .Select(async x => await FileHelper.PathToFile(x)).ToList();
        await Task.WhenAll(testConversion);
    }

    [Benchmark]

    public async Task CreateFileSetAsParallel()
    {

        Random rng = new Random();
        var testFiles = Enumerable.Range(1, 300).Select(x => $"testdata/test_file_{x}.avi");
        var testFileCreation = testFiles.AsParallel().WithCancellation(_tokenSource.Token)
                .Select(async x => await FileHelper.CreateFile(x, rng.Next(5000), (byte)'w')).ToList();
        await Task.WhenAll(testFileCreation);

        var testConversion = testFiles
            .AsParallel().WithCancellation(_tokenSource.Token)
            .Select(async x => await FileHelper.PathToFile(x)).ToList();

        var testData = await Task.WhenAll(testConversion);

    }
}