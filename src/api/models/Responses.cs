using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using shared;
using System.Globalization;
using System.Text.Json;

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

public record MatchQueries
{
    public List<MatchScore<shared.TMDB.Models.Result>> Results { get; set; } = new();
}

public record Matches
{
    public List<MatchScore<MediaReferences>> Suggestions { get; set; }
}

public record MediaReferences : shared.data.File
{
    public List<shared.TMDB.Models.MovieDetailsResponse> References { get; set; } = new();

    [SetsRequiredMembers]
    public MediaReferences(shared.data.File file)
    {
        path_hash = file.path_hash;
        path = file.path;
        date_modified = file.date_modified;
        date_created = file.date_created;
        size = file.size;
        extension = file.extension;
        hash = file.hash;
        attributes = file.attributes;
        extra_attributes = file.extra_attributes;
    }
}

public class Error
{
    public string Message { get; init; } = "";
}

public record MediaUpdateStatus
{
    [Precision(2)]
    public decimal PercentComplete { get; init; } = 0.00m;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UpdateState State { get; init; } = UpdateState.NotStarted;
    public string? TaskStatus { get; init; } = "NotCreated";
    public string? Message { get; init; } = "";

    public MediaUpdateStatus(UpdateStatus status, TaskStatus? taskStatus = default, string? message = default)
    {
        PercentComplete = status.TotalFiles > 0 ? Decimal.Divide(status.FilesProcessed, status.TotalFiles) * 100m : 0;
        State = status.State;
        TaskStatus = taskStatus?.ToString();
        Message = message;
    }
}

public class DecimalPrecisionConverter : JsonConverter<decimal>
{
    private readonly string _format;

    // Pass the desired number of decimal places here
    public DecimalPrecisionConverter(int decimalPlaces)
    {
        _format = "0." + new string('0', decimalPlaces);
    }

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(value.ToString(_format, CultureInfo.InvariantCulture));
    }
}


public class PrecisionAttribute : JsonConverterAttribute
{
    private readonly int _decimalPlaces;

    public PrecisionAttribute(int decimalPlaces)
    {
        _decimalPlaces = decimalPlaces;
    }

    public override JsonConverter CreateConverter(Type typeToConvert)
    {
        return new DecimalPrecisionConverter(_decimalPlaces);
    }
}
