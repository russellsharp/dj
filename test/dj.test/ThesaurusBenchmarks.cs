using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using dj.benchmarks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using shared.thesaurus;
using Xunit.Internal;

namespace dj.test
{
    [MemoryDiagnoser]
    public class ThesaurusBenchmarks()
    {
        private static ThesaurusConfiguration thesaurusConfigDefaults = new ThesaurusConfiguration()
        {
            DictionaryPath = "wordnet/staticdata/",
            DatabasePath = "wordnet/database/wordnet.db"
        };

        private Thesaurus _thesaurus = new(Options.Create<ThesaurusConfiguration>(thesaurusConfigDefaults), new LoggerFactory().CreateLogger<Thesaurus>());

        const string BaseWord = "Choose";

        [GlobalSetup]
        public void Setup()
        {
        }

        [Benchmark]
        public void SqliteSearch()
        {
            var results = _thesaurus.Search(BaseWord);
        }

        [Benchmark]
        public void StaticFilesSearch()
        {
            _thesaurus.Initialize();
            var results = _thesaurus.Search(BaseWord);
        }
    }
}