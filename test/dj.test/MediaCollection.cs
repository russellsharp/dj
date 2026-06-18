using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using shared;
using shared.data;
using Xunit.Internal;

namespace dj.test
{
    public class MediaCollection
    {
        private IOptions<MediaReaderConfiguration> BasicConfig = Options.Create(new MediaReaderConfiguration
        {
            Filter = "*.*",
            BaseDirectory = "testMedia",
            DirectoryRecursionDepth = 50,
            AudioExtensions = @"mp3",
            VideoExtensions = @"avi;mkv",
        });

        private IOptions<DatabaseConfiguration> BasicDatabaseConfig = Options.Create(new DatabaseConfiguration
        {
            DataFile = "testdata/testmedia.db",

        });

        private CancellationTokenSource _tokenSource = new();

        [Fact]
        public async Task MatchLocalFiles()
        {
            IDatabase db = new shared.data.Database(BasicDatabaseConfig);
            IMediaCollection media = new shared.MediaCollection(BasicConfig, db);

            db.EnsureConnected();
            await db.Truncate();

            await media.UpdateRepos(BasicConfig.Value.BaseDirectory, _tokenSource.Token);

            var keywords = SearchHelpers.SanitizeForSearch("Training Day", false, _tokenSource.Token);

            var matches = await media.Match(keywords, _tokenSource.Token);

            matches.Should().BeEmpty();

            matches.ForEach(x => Debug.WriteLine(x.path));

            keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", false, _tokenSource.Token);

            matches = await media.Match(keywords, _tokenSource.Token);

            matches.Should().NotBeEmpty();

            matches.ForEach(x => Debug.WriteLine(x.path));
        }
    }
}