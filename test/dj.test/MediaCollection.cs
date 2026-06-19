using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Extensions.Options;
using Perfolizer.Horology;
using shared;
using shared.data;
using shared.TMDB;
using shared.TMDB.Models;
using Xunit.Internal;

namespace dj.test
{
    public class MediaCollection
    {
        private IOptions<MediaReaderConfiguration> BasicConfig = Options.Create(new MediaReaderConfiguration
        {
            Filter = "*.*",
            BaseDirectory = "C:/dev/mediaReference",
            DirectoryRecursionDepth = 50,
            AudioExtensions = @"mp3",
            VideoExtensions = @"avi;mkv;mp4",
        });

        private IOptions<DatabaseConfiguration> BasicDatabaseConfig = Options.Create(new DatabaseConfiguration
        {
            DataFile = "testdata/testmedia.db",

        });

        private static IOptions<EndpointConfig> BasicEndpointOptions = Options.Create(new EndpointConfig
        {
            BaseUrl = "https://api.themoviedb.org/3",
            ApiKey = Repo.SUPER_SECRET_API_KEY,
            DatabasePath = "testdata/tmdb.db",
            RequestLimit = 40,
            RequestWindowSeconds = 10,
            TitleWeight = 100,
            OverviewWeight = 1
        });

        private CancellationTokenSource _tokenSource = new();

        [Fact]
        public async Task MatchLocalFiles()
        {
            IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
            ITMDB tmdb = new shared.TMDB.TMDB(repo);
            IDatabase db = new shared.data.Database(BasicDatabaseConfig);
            IMediaCollection media = new shared.MediaCollection(BasicConfig, db, tmdb);

            db.EnsureConnected();

            await media.UpdateRepos(BasicConfig.Value.BaseDirectory, _tokenSource.Token);

            var keywords = SearchHelpers.SanitizeForSearch("Training Day", _tokenSource.Token, 3, false);

            var matches = (await media.Match<shared.data.File>(keywords, _tokenSource.Token)).Select(x => x.Details);

            matches.Should().BeEmpty();

            matches.ForEach(x => Debug.WriteLine(x.path));

            keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _tokenSource.Token, 3, false);

            var matcheScores = (await media.Match<shared.data.File>(keywords, _tokenSource.Token)).ToList();

            matcheScores.Should().NotBeEmpty();

            matcheScores.ForEach(x => Debug.WriteLine(x.Hits));
        }

        [Fact]
        public async Task MatchLocalFilesDictionaryAction()
        {
            IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
            ITMDB tmdb = new shared.TMDB.TMDB(repo);
            IDatabase db = new shared.data.Database(BasicDatabaseConfig);
            IMediaCollection media = new shared.MediaCollection(BasicConfig, db, tmdb);

            await media.Initialize(_tokenSource.Token);

            var keywords = SearchHelpers.SanitizeForSearch("Inglourious Basterds", _tokenSource.Token, 3, false);

            var matcheScores = (await media.Match<shared.data.File>(keywords, _tokenSource.Token)).ToList();

            matcheScores.Should().NotBeEmpty();

            matcheScores.Select(x => x.Details).ForEach(x => Debug.WriteLine(x.path));
        }

        [Fact]
        public async Task MatchLocalFilesToTmdb()
        {
            IRepo repo = new shared.TMDB.Repo(BasicEndpointOptions, new Cache(BasicEndpointOptions), _tokenSource);
            ITMDB tmdb = new shared.TMDB.TMDB(repo);
            IDatabase db = new shared.data.Database(BasicDatabaseConfig);
            IMediaCollection media = new shared.MediaCollection(BasicConfig, db, tmdb);

            // await media.UpdateRepos(BasicConfig.Value.BaseDirectory, _tokenSource.Token);

            await media.Initialize(_tokenSource.Token);

            var localMovies = await media.Files(MediaType.Video);

            localMovies.Should().NotBeNullOrEmpty();

            int a = 0;
            var movieTitle = a == 0 ? "Training Day" : "Inglourious Basterds";
            var localMovie = localMovies.FirstOrDefault(x => x.path.Contains(movieTitle));

            localMovie.Should().NotBeNull();

            var keywords = SearchHelpers.SanitizeForSearch(localMovie.path, _tokenSource.Token, 3, false);

            var pathDepthMin = 3;
            var pathDepthMax = 5;
            var minimumResultCount = 1;

            //parse path for segments and select relevant portions
            var pathForQuery = Path.ChangeExtension(localMovie.path, null);
            var pathSegments = pathForQuery.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var relevantPathSegments = pathDepthMin > 0 ? pathSegments.TakeLast(pathDepthMin) : pathSegments;

            relevantPathSegments.ToList().ForEach(x => Debug.WriteLine($"All: {x}"));

            var tmdbResults = new List<shared.TMDB.Models.Result>();

            foreach (var segment in relevantPathSegments)
            {
                var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(segment, _tokenSource.Token, -1, true));
                if (!string.IsNullOrWhiteSpace(sanitized) && sanitized.Length > 2)
                {
                    Debug.WriteLine($"min segments: {sanitized}");
                    tmdbResults.AddRange((await tmdb.QueryMovies(sanitized)).results);
                }
            }

            if (tmdbResults.Count() < minimumResultCount)
            {
                int start = Math.Clamp(pathSegments.Count() - pathDepthMax, 0, pathSegments.Count() - pathDepthMin);
                int end = Math.Clamp(pathSegments.Count() - pathDepthMin, 0, pathSegments.Count() - pathDepthMin);
                for (int i = start; i <= end; i++)
                {
                    var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(pathSegments[i], _tokenSource.Token, -1, true));
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        Debug.WriteLine(sanitized);
                        tmdbResults.AddRange((await tmdb.QueryMovies(sanitized)).results);
                    }
                }
            }

            //still have no results so skip dictionary check
            if (tmdbResults.Count() < minimumResultCount)
            {
                foreach (var segment in relevantPathSegments)
                {
                    var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(segment, _tokenSource.Token, -1, false));
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        Debug.WriteLine($"min segments: {sanitized}");
                        tmdbResults.AddRange((await tmdb.QueryMovies(sanitized)).results);
                    }
                }

                if (tmdbResults.Count() < minimumResultCount)
                {
                    int start = Math.Clamp(pathSegments.Count() - pathDepthMax, 0, pathSegments.Count() - pathDepthMin);
                    int end = Math.Clamp(pathSegments.Count() - pathDepthMin, 0, pathSegments.Count() - pathDepthMin);
                    for (int i = start; i <= end; i++)
                    {
                        var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(pathSegments[i], _tokenSource.Token, -1, false));

                        if (!string.IsNullOrWhiteSpace(sanitized))
                        {
                            Debug.WriteLine(sanitized);
                            tmdbResults.AddRange((await tmdb.QueryMovies(sanitized)).results);
                        }
                    }
                }
            }

            tmdbResults.Should().NotBeNull();

            tmdbResults.ForEach(x => Debug.WriteLine($"{x.id} - {x.title}"));
        }
    }
}