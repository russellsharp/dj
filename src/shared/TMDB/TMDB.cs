using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using shared.TMDB.Models;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing.Internal;

namespace shared.TMDB;

public interface ITMDB : IDisposable
{
    Task<MovieDetailsResponse?> GetMovie(int id);
    Task<MovieQueryResponse?> QueryTitle(string query, int page = 1, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryOverviews(string query, int minimumHitCount, CancellationToken? token = null);
    Task<IEnumerable<MatchScore<MovieDetailsResponse>>> QueryWithGroupedTerms(IEnumerable<IEnumerable<string>> query, int minimumHitCount, CancellationToken? token = null);
    Task<IEnumerable<Result?>> PathToTmdb(string filePath, MatchingContext context, bool useDictionary = true, CancellationToken? token = null);
    Task Populate(IEnumerable<string> paths, MatchingContext context, bool useDictionary = false, CancellationToken? token = null);
    UpdateStatus Status { get; }
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

public class TMDB : ITMDB, IDisposable
{
    private IRepo _repo;
    private CancellationTokenSource _cts;

    public TMDB(IRepo repo, CancellationTokenSource tokenSource)
    {
        _repo = repo;
        _cts = tokenSource;
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

    public async Task<MovieQueryResponse?> QueryTitle(string query, int page = 1, CancellationToken? token = null)
    {
        return await _repo.QueryTitle(query, page, token);
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
        token ??= _cts.Token;

        List<Result?> matches = new();

        try
        {
            token.Value.ThrowIfCancellationRequested();

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
            await MatchSegments(relevantPathSegments.ToArray(), tmdbResults, useDictionary, token);

            matches = BestMatch(relevantPathSegments, tmdbResults, minimumMatchScore).ToList();

            token.Value.ThrowIfCancellationRequested();

            if (matches is not null)
            {
                Interlocked.Increment(ref _filesQueried);
                return matches;
            }

            //try less relevant with dictionary
            tmdbResults.Clear();

            await MatchSegments(lessRelevantPathSegments, tmdbResults, useDictionary, token);

            matches = BestMatch(relevantPathSegments, tmdbResults, minimumMatchScore).ToList();

            Interlocked.Increment(ref _filesQueried);

            token.Value.ThrowIfCancellationRequested();

            return matches;
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
        {
            Console.WriteLine("Population task canceled.");
            Interlocked.Exchange(ref _updateState, (long)UpdateState.Canceled);
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while querying TMDB for: {filePath}\n{ex}");
            Interlocked.Exchange(ref _updateState, (long)UpdateState.Errored);
            throw;
        }
        finally
        {
            Console.WriteLine($"{(_totalFilesToQuery > 0 ? (decimal)_filesQueried / (decimal)_totalFilesToQuery * 100.0m : 0):F2}%");
        }
    }

    private async Task MatchSegments(string[] pathSegments, List<Result> tmdbResults, bool useDictionary = true, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            foreach (var segment in pathSegments)
            {
                token.Value.ThrowIfCancellationRequested();

                var sanitized = string.Join(' ', SearchHelpers.SanitizeForSearch(segment, _cts.Token, useDictionary));

                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    var queryResults = await QueryTitle(sanitized, 1, token);

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
        catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
        {
            Console.WriteLine("Matching canceled.");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while matching path segments: {string.Join(Path.PathSeparator, pathSegments)}");
            throw;
        }
    }

    private long _totalFilesToQuery = 0;
    private long _filesQueried = 0;
    private long _updateState = 0;

    public async Task Populate(IEnumerable<string> paths, MatchingContext context, bool useDictionary = false, CancellationToken? token = null)
    {
        token ??= _cts.Token;

        try
        {
            if ((UpdateState)Interlocked.Read(ref _updateState) == UpdateState.Running)
            {
                throw new InvalidOperationException("TMDB populate task already running.");
            }
            Interlocked.Exchange(ref _totalFilesToQuery, paths.LongCount());
            Interlocked.Exchange(ref _filesQueried, 0);
            Interlocked.Exchange(ref _updateState, (long)UpdateState.Running);
            var paralllelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = token.Value };
            await Parallel.ForEachAsync(paths, paralllelOptions, async (file, results) => { await PathToTmdb(file, context, useDictionary, token.Value); });
            Interlocked.Exchange(ref _updateState, (long)UpdateState.Complete);
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
        {
            Console.WriteLine("Population process canceled.");
            Interlocked.Exchange(ref _updateState, (long)UpdateState.Canceled);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while querying for TMDB data.");
            Interlocked.Exchange(ref _updateState, (long)UpdateState.Errored);
        }
        finally
        {
        }
    }

    public UpdateStatus Status
    {
        get
        {
            return new UpdateStatus
            {
                State = (UpdateState)Interlocked.Read(ref _updateState),
                FilesProcessed = Interlocked.Read(ref _filesQueried),
                TotalFiles = Interlocked.Read(ref _totalFilesToQuery)
            };
        }
    }



    #region IDisposable
    private int _disposed = 0;
    public void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            _repo?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dispose TMDB repository:\r\n{ex}");
            throw;
        }
    }
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TMDB()
    {
        Dispose(false);
    }
    #endregion IDisposable
}