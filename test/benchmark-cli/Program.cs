using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using shared;
using dj.test;
using BenchmarkDotNet.Exporters;
using System.Diagnostics;
using BenchmarkDotNet.Loggers;

var cts = new CancellationTokenSource();
var summary = BenchmarkRunner.Run<ThesaurusBenchmarks>();
await MarkdownExporter.Default.ExportAsync(summary, ConsoleLogger.Default, cts.Token);
summary.Reports.ToList().ForEach(x => Debug.WriteLine(x.ResultStatistics.ToString()));