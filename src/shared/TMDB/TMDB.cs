using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared.TMDB.Models;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing.Internal;

namespace shared.TMDB
{
    public interface ITMDB
    {
        Task<MovieDetailsResponse?> GetMovie(int id);
        Task<MovieQueryResponse> QueryMovies(string query, int page = 1);
        List<Genre> GetGenres();
        Task<IEnumerable<Result?>> PathToTmdb(string filePath, MatchingContext context, bool useDictionary = true, CancellationToken? token = null);
    }

    public class MatchingContext
    {
        public int MinimumScore { get; set; } = 100;
        public int PathDepthMin { get; set; } = 2;
        public int PathDepthMax { get; set; } = 5;
    }

    public class TMDB : ITMDB
    {
        private IRepo _repo;
        private CancellationTokenSource _tokenSource;

        public TMDB(IRepo repo, CancellationTokenSource tokenSource)
        {
            _repo = repo;
            _tokenSource = tokenSource;
        }

        public List<Genre> GetGenres()
        {
            return _repo.MovieGenres().Genres;
        }

        public async Task<MovieDetailsResponse?> GetMovie(int id)
        {
            var result = _repo.TryMovie(id, out MovieDetailsResponse? movie);
            Debug.Assert(result);
            return movie;
        }

        public async Task<MovieQueryResponse> QueryMovies(string query, int page = 1)
        {
            return await _repo.QueryTitle(query, page);
        }

        private IEnumerable<Result?> BestMatch(IEnumerable<string> pathSegments, IEnumerable<Result> tmdbResults, double minimumScore = 100)
        {
            //Levenshtein scoring of matches
            var scoredResultsByTitle = new List<MatchScore<Result>>();
            foreach (var pathSegment in pathSegments)
            {
                scoredResultsByTitle.AddRange(tmdbResults.Select(x => new MatchScore<Result> { Hits = SearchHelpers.Levenshtein(pathSegment, x.title), Details = x }));
            }

            return scoredResultsByTitle.Where(x => x.Hits >= minimumScore).OrderByDescending(x => x.Hits).Select(x => x.Details);
        }

        public async Task<IEnumerable<Result?>> PathToTmdb(string filePath, MatchingContext context, bool useDictionary = true, CancellationToken? token = null)
        {
            token ??= _tokenSource.Token;

            List<Result?> matches = null;

            const int minimumMatchScore = 100;

            //parse path for segments and select relevant portions
            var pathForQuery = SearchHelpers.SanitizePath(Path.ChangeExtension(filePath, null));
            var pathSegments = pathForQuery.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var relevantPathSegments = context.PathDepthMin > 0 ? pathSegments.TakeLast(context.PathDepthMin) : pathSegments;

            //parse path for remaining segments within maximum depth
            int start = Math.Clamp(pathSegments.Count() - context.PathDepthMax, 0, pathSegments.Count() - context.PathDepthMin);
            int end = Math.Clamp(pathSegments.Count() - context.PathDepthMin, 0, pathSegments.Count() - context.PathDepthMin);
            var lessRelevantPathSegments = pathSegments[start..end];

            // relevantPathSegments.ToList().ForEach(x => Debug.WriteLine($"Relevant: {x}"));
            // lessRelevantPathSegments.ToList().ForEach(x => Debug.WriteLine($"Less relevant: {x}"));

            var tmdbResults = new List<shared.TMDB.Models.Result>();

            //try to find match with relevant path segments and dictionary
            await MatchSegments(relevantPathSegments.ToArray(), tmdbResults, useDictionary);

            matches = BestMatch(relevantPathSegments, tmdbResults, minimumMatchScore).ToList();

            if (matches is not null) return matches;

            //try less relevant with dictionary
            tmdbResults.Clear();

            await MatchSegments(lessRelevantPathSegments, tmdbResults, useDictionary);

            matches = BestMatch(relevantPathSegments, tmdbResults, minimumMatchScore).ToList();

            return matches;
        }

        private async Task MatchSegments(string[] pathSegments, List<Result> tmdbResults, bool useDictionary = true)
        {
            foreach (var segment in pathSegments)
            {
                var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(segment, _tokenSource.Token, -1, useDictionary));

                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    var queryResults = await QueryMovies(sanitized);

                    if (queryResults.results is null)
                    {
                        Debug.WriteLine($"{string.Join(' ', pathSegments)} got null results from TMDB.");
                    }
                    else
                    {
                        tmdbResults.AddRange(queryResults.results);
                    }
                }
            }
        }
    }
}