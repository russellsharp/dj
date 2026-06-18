using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using shared.TMDB;
using shared.TMDB.Models;

namespace shared.TMDB;

public class ScoringConfig
{
    public int PointsPerKeywordHit { get; init; } = 5;
}

public class Scorer
{
    private readonly IRepo _repo;
    private IOptions<ScoringConfig> _config { get; }
    private readonly ICache _cache;

    public Scorer(IOptions<ScoringConfig> config, IRepo repo, ICache cache)
    {
        _config = config;
        _repo = repo;
        _cache = cache;
    }

    public int Score(string[] keywords, CancellationToken token)
    {
        return 0;
    }
}