using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using shared.thesaurus;
using Xunit.Internal;

namespace dj.test
{
    public class ThesaurusTests
    {
        [Fact]
        public async Task GetSynonyms()
        {
            var thesus = new Thesaurus();
            thesus.Initialize();

            var baseWord = "day";
            var related = await thesus.Search(baseWord);

            related.Should().NotBeNullOrEmpty();

            related.ForEach(x => Debug.WriteLine(x));
        }
    }
}