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
        Task<MovieQueryResponse?> QueryTitle(string query, int page = 1);
        Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(string query, int minimumHitCount, CancellationToken? token = null);
        Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null);
        Task<IEnumerable<Result?>> PathToTmdb(string filePath, MatchingContext context, bool useDictionary = true, CancellationToken? token = null);
    }

    public class MatchingContext
    {
        public int MinimumScore { get; set; } = 100;
        public int PathDepthMin { get; set; } = 2;
        public int PathDepthMax { get; set; } = 5;

        public bool Validate()
        {
            if (PathDepthMin > PathDepthMax)
            {
                return false;
            }

            return true;
        }
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

        public async Task<List<Genre>> GetGenres()
        {
            var genresResponse = await _repo.MovieGenres();
            return genresResponse?.Genres ?? new List<Genre>();
        }

        public async Task<MovieDetailsResponse?> GetMovie(int id)
        {
            return await _repo.Movie(id);
        }

        public async Task<MovieQueryResponse?> QueryTitle(string query, int page = 1)
        {
            return await _repo.QueryTitle(query, page);
        }

        public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(string query, int minimumHitCount, CancellationToken? token = null)
        {
            return await _repo.QueryOverviews(query, minimumHitCount, token);
        }

        public async Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null)
        {
            return await _repo.QueryWithGroupedTerms(query, minimumHitCount, token);
        }

        private IEnumerable<Result?> BestMatch(IEnumerable<string> pathSegments, IEnumerable<Result> tmdbResults, double minimumScore = 100)
        {
            //Levenshtein scoring of matches
            var scoredResultsByTitle = new List<MatchScore<Result>>();
            foreach (var pathSegment in pathSegments)
            {
                scoredResultsByTitle.AddRange(tmdbResults.Select(x => new MatchScore<Result> { Hits = Scoring.Levenshtein(pathSegment, x.title ?? string.Empty), Details = x }));
            }

            return scoredResultsByTitle.Where(x => x.Hits >= minimumScore).OrderByDescending(x => x.Hits).Select(x => x.Details);
        }

        public async Task<IEnumerable<Result?>> PathToTmdb(string filePath, MatchingContext context, bool useDictionary = true, CancellationToken? token = null)
        {
            token ??= _tokenSource.Token;

            List<Result?> matches = new();

            const int minimumMatchScore = 100;

            //parse path for segments and select relevant portions
            var pathForQuery = SearchHelpers.SanitizeString(Path.ChangeExtension(filePath, null));
            var pathSegments = pathForQuery.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var relevantPathSegments = context.PathDepthMin > 0 ? pathSegments.TakeLast(context.PathDepthMin) : pathSegments;

            //parse path for remaining segments within maximum depth
            int start = Math.Clamp(pathSegments.Count() - context.PathDepthMax, 0, pathSegments.Count() - context.PathDepthMin);
            int end = Math.Clamp(pathSegments.Count() - context.PathDepthMin, 0, pathSegments.Count() - context.PathDepthMin);
            var lessRelevantPathSegments = pathSegments[start..end];

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
                var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(segment, _tokenSource.Token, useDictionary));

                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    var queryResults = await QueryTitle(sanitized);

                    if (queryResults?.results is null)
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