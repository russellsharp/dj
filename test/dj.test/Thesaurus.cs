using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared.thesaurus;
using Xunit.Internal;

namespace dj.test
{
    public class ThesaurusTests
    {
        private static ThesaurusConfiguration thesaurusConfigDefaults = new ThesaurusConfiguration()
        {
            DictionaryPath = "wordnet/staticdata/",
            DatabasePath = "wordnet/database/wordnet.db"
        };


        [Fact]
        public async Task GetSynonyms()
        {
            var thesus = new Thesaurus(Options.Create(thesaurusConfigDefaults));
            thesus.Initialize();

            var baseWord = "choose";
            var related = await thesus.Search(baseWord);

            related.Should().NotBeNullOrEmpty();

            related.ForEach(x => Debug.WriteLine(x));
        }

        [Fact(Skip = "Used to build the database.")]
        public async Task ImportJsonl()
        {
            var thesus = new Thesaurus(Options.Create(thesaurusConfigDefaults));
            thesus.Initialize();

            await thesus.ImportFromJsonl(@"wordnet/staticdata/en_thesaurus.jsonl");
        }

        [Fact]
        public async Task SearchDatabase()
        {
            var thesus = new Thesaurus(Options.Create(thesaurusConfigDefaults));
            thesus.Initialize();

            var baseWord = "choose";
            var synonyms = await thesus.Search(baseWord);

            synonyms.Should().NotBeNullOrEmpty();

            synonyms.ForEach(x => Debug.WriteLine(x));
        }
    }
}