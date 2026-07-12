using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using shared;
using dj.test;
using BenchmarkDotNet.Exporters;
using System.Diagnostics;
using BenchmarkDotNet.Loggers;

var summary = BenchmarkRunner.Run<ThesaurusBenchmarks>();
MarkdownExporter.Default.ExportToLog(summary, ConsoleLogger.Default);
summary.Reports.ToList().ForEach(x => Debug.WriteLine(x.ResultStatistics.ToString()));