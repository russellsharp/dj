
using System.Text.Json.Serialization;
using shared;

namespace api.models;

public record QueryResults
{
    public List<Media> Media { get; set; }
}

public record Media
{
    public string FilePath { get; set; }
    public string Title { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MediaType Type { get; set; }
    public int Hits { get; set; }
}

public record TMDBResults
{
    public List<TMDBSummary> Media { get; init; }
}

public record TMDBSummary
{
    public int Id { get; init; }
    public string Title { get; init; }
    public double Rank { get; init; }
    public string Overview { get; init; }
    public MediaType Type { get; init; }
}

public record TMDBDetailResults
{
    public List<TMDBDetails> Media { get; init; }
}
public record TMDBDetails : TMDBSummary
{
    public string ImdbId { get; init; }
}

public record MediaFiles
{
    public List<shared.data.File> Files { get; init; }
}

public record Matches
{
    public List<MatchScore<shared.data.File>> Suggestions { get; init; }
}