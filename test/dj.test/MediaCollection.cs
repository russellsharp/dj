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
using shared.TMDB.Models;
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

            await media.UpdateRepos(BasicConfig.Value.BaseDirectory, _tokenSource.Token);

            var keywords = SearchHelpers.SanitizeForSearch("Training Day", false, _tokenSource.Token);

            var matches = (await media.Match<shared.data.File>(keywords, _tokenSource.Token)).Select(x => x.Details);

            matches.Should().BeEmpty();

            matches.ForEach(x => Debug.WriteLine(x.path));

            keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", false, _tokenSource.Token);

            var matcheScores = (await media.Match<shared.data.File>(keywords, _tokenSource.Token)).ToList();

            matcheScores.Should().NotBeEmpty();

            matcheScores.ForEach(x => Debug.WriteLine(x.Hits));
        }

        [Fact]
        public async Task MatchLocalFilesDictionaryAction()
        {
            IDatabase db = new shared.data.Database(BasicDatabaseConfig);
            IMediaCollection media = new shared.MediaCollection(BasicConfig, db);

            await media.Initialize(_tokenSource.Token);

            var keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", false, _tokenSource.Token);

            var matcheScores = (await media.Match<shared.data.File>(keywords, _tokenSource.Token)).ToList();

            matcheScores.Should().NotBeEmpty();

            matcheScores.Select(x => x.Details as shared.data.File).ForEach(x => Debug.WriteLine(x.path));
        }
    }
}