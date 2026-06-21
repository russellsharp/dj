using System.Text.Json.Serialization;

namespace shared.TMDB.Models;

public class GenreResponse
{
    [JsonPropertyName("genres")]
    public List<Genre> Genres { get; set; }
}

public class Genre
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Result
{
    [JsonPropertyName("adult")]
    public bool? adult { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string backdrop_path { get; set; }

    [JsonPropertyName("genre_ids")]
    public List<int?> genre_ids { get; set; }

    [JsonPropertyName("id")]
    public int? id { get; set; }

    [JsonPropertyName("title")]
    public string title { get; set; }

    [JsonPropertyName("original_language")]
    public string original_language { get; set; }

    [JsonPropertyName("original_title")]
    public string original_title { get; set; }

    [JsonPropertyName("overview")]
    public string overview { get; set; }

    [JsonPropertyName("popularity")]
    public double? popularity { get; set; }

    [JsonPropertyName("poster_path")]
    public string poster_path { get; set; }

    [JsonPropertyName("release_date")]
    public string release_date { get; set; }

    [JsonPropertyName("softcore")]
    public bool? softcore { get; set; }

    [JsonPropertyName("video")]
    public bool? video { get; set; }

    [JsonPropertyName("vote_average")]
    public double? vote_average { get; set; }

    [JsonPropertyName("vote_count")]
    public int? vote_count { get; set; }
}

public class MovieQueryResponse
{
    [JsonPropertyName("page")]
    public int? page { get; set; }

    [JsonPropertyName("results")]
    public List<Result> results { get; set; }

    [JsonPropertyName("total_pages")]
    public int? total_pages { get; set; }

    [JsonPropertyName("total_results")]
    public int? total_results { get; set; }
}

public class BelongsToCollection
{
    [JsonPropertyName("id")]
    public int? id { get; set; }

    [JsonPropertyName("name")]
    public string name { get; set; }

    [JsonPropertyName("poster_path")]
    public string poster_path { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string backdrop_path { get; set; }
}

public class ProductionCompany
{
    [JsonPropertyName("id")]
    public int? id { get; set; }

    [JsonPropertyName("logo_path")]
    public string logo_path { get; set; }

    [JsonPropertyName("name")]
    public string name { get; set; }

    [JsonPropertyName("origin_country")]
    public string origin_country { get; set; }
}

public class ProductionCountry
{
    [JsonPropertyName("iso_3166_1")]
    public string iso_3166_1 { get; set; }

    [JsonPropertyName("name")]
    public string name { get; set; }
}

public class MovieDetailsResponse
{
    [JsonPropertyName("adult")]
    public bool? adult { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string backdrop_path { get; set; }

    [JsonPropertyName("belongs_to_collection")]
    public BelongsToCollection belongs_to_collection { get; set; }

    [JsonPropertyName("budget")]
    public int? budget { get; set; }

    [JsonPropertyName("genres")]
    public List<Genre> genres { get; set; }

    [JsonPropertyName("homepage")]
    public string homepage { get; set; }

    [JsonPropertyName("id")]
    public int id { get; set; }

    [JsonPropertyName("imdb_id")]
    public string imdb_id { get; set; }

    [JsonPropertyName("origin_country")]
    public List<string> origin_country { get; set; }

    [JsonPropertyName("original_language")]
    public string original_language { get; set; }

    [JsonPropertyName("original_title")]
    public string original_title { get; set; }

    [JsonPropertyName("overview")]
    public string overview { get; set; }

    [JsonPropertyName("popularity")]
    public double? popularity { get; set; }

    [JsonPropertyName("poster_path")]
    public string poster_path { get; set; }

    [JsonPropertyName("production_companies")]
    public List<ProductionCompany> production_companies { get; set; }

    [JsonPropertyName("production_countries")]
    public List<ProductionCountry> production_countries { get; set; }

    [JsonPropertyName("release_date")]
    public string release_date { get; set; }

    [JsonPropertyName("revenue")]
    public int? revenue { get; set; }

    [JsonPropertyName("runtime")]
    public int? runtime { get; set; }

    [JsonPropertyName("spoken_languages")]
    public List<SpokenLanguage> spoken_languages { get; set; }

    [JsonPropertyName("status")]
    public string status { get; set; }

    [JsonPropertyName("tagline")]
    public string tagline { get; set; }

    [JsonPropertyName("title")]
    public string title { get; set; }

    [JsonPropertyName("video")]
    public bool? video { get; set; }

    [JsonPropertyName("vote_average")]
    public double? vote_average { get; set; }

    [JsonPropertyName("vote_count")]
    public int? vote_count { get; set; }
}

public class SpokenLanguage
{
    [JsonPropertyName("english_name")]
    public string english_name { get; set; }

    [JsonPropertyName("iso_639_1")]
    public string iso_639_1 { get; set; }

    [JsonPropertyName("name")]
    public string name { get; set; }
}

