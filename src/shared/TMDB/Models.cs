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
    public bool? adult;

    [JsonPropertyName("backdrop_path")]
    public string backdrop_path;

    [JsonPropertyName("genre_ids")]
    public List<int?> genre_ids;

    [JsonPropertyName("id")]
    public int? id;

    [JsonPropertyName("title")]
    public string title;

    [JsonPropertyName("original_language")]
    public string original_language;

    [JsonPropertyName("original_title")]
    public string original_title;

    [JsonPropertyName("overview")]
    public string overview;

    [JsonPropertyName("popularity")]
    public double? popularity;

    [JsonPropertyName("poster_path")]
    public string poster_path;

    [JsonPropertyName("release_date")]
    public string release_date;

    [JsonPropertyName("softcore")]
    public bool? softcore;

    [JsonPropertyName("video")]
    public bool? video;

    [JsonPropertyName("vote_average")]
    public double? vote_average;

    [JsonPropertyName("vote_count")]
    public int? vote_count;
}

public class MovieQueryResponse
{
    [JsonPropertyName("page")]
    public int? page;

    [JsonPropertyName("results")]
    public List<Result> results;

    [JsonPropertyName("total_pages")]
    public int? total_pages;

    [JsonPropertyName("total_results")]
    public int? total_results;
}

public class BelongsToCollection
{
    [JsonPropertyName("id")]
    public int? id;

    [JsonPropertyName("name")]
    public string name;

    [JsonPropertyName("poster_path")]
    public string poster_path;

    [JsonPropertyName("backdrop_path")]
    public string backdrop_path;
}

public class ProductionCompany
{
    [JsonPropertyName("id")]
    public int? id;

    [JsonPropertyName("logo_path")]
    public string logo_path;

    [JsonPropertyName("name")]
    public string name;

    [JsonPropertyName("origin_country")]
    public string origin_country;
}

public class ProductionCountry
{
    [JsonPropertyName("iso_3166_1")]
    public string iso_3166_1;

    [JsonPropertyName("name")]
    public string name;
}

public class MovieDetailsResponse
{
    [JsonPropertyName("adult")]
    public bool? adult;

    [JsonPropertyName("backdrop_path")]
    public string backdrop_path;

    [JsonPropertyName("belongs_to_collection")]
    public BelongsToCollection belongs_to_collection;

    [JsonPropertyName("budget")]
    public int? budget;

    [JsonPropertyName("genres")]
    public List<Genre> genres;

    [JsonPropertyName("homepage")]
    public string homepage;

    [JsonPropertyName("id")]
    public int id;

    [JsonPropertyName("imdb_id")]
    public string imdb_id;

    [JsonPropertyName("origin_country")]
    public List<string> origin_country;

    [JsonPropertyName("original_language")]
    public string original_language;

    [JsonPropertyName("original_title")]
    public string original_title;

    [JsonPropertyName("overview")]
    public string overview;

    [JsonPropertyName("popularity")]
    public double? popularity;

    [JsonPropertyName("poster_path")]
    public string poster_path;

    [JsonPropertyName("production_companies")]
    public List<ProductionCompany> production_companies;

    [JsonPropertyName("production_countries")]
    public List<ProductionCountry> production_countries;

    [JsonPropertyName("release_date")]
    public string release_date;

    [JsonPropertyName("revenue")]
    public int? revenue;

    [JsonPropertyName("runtime")]
    public int? runtime;

    [JsonPropertyName("spoken_languages")]
    public List<SpokenLanguage> spoken_languages;

    [JsonPropertyName("status")]
    public string status;

    [JsonPropertyName("tagline")]
    public string tagline;

    [JsonPropertyName("title")]
    public string title;

    [JsonPropertyName("video")]
    public bool? video;

    [JsonPropertyName("vote_average")]
    public double? vote_average;

    [JsonPropertyName("vote_count")]
    public int? vote_count;
}

public class SpokenLanguage
{
    [JsonPropertyName("english_name")]
    public string english_name;

    [JsonPropertyName("iso_639_1")]
    public string iso_639_1;

    [JsonPropertyName("name")]
    public string name;
}

