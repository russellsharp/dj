using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Globalization;
using Dapper;

namespace shared.data;

[Table("file")]
public record File
{
    public string? path_hash { get; init; }
    public required string path { get; init; }
    public required DateTime date_modified { get; init; }
    public required DateTime date_created { get; init; }
    public required long size { get; init; }
    public required string extension { get; init; }
    public string? hash { get; init; }
    public string? attributes { get; init; }
    public string? extra_attributes { get; init; }
}

[Table("users")]
public record Users
{
    public required string user_name { get; init; }
    public string display_name { get; init; }
    public string client_id { get; init; }
    public string scopes { get; init; }
    public string password_hash { get; init; }
    public DateTime create_at { get; init; }
}

public class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        // When sending to the database, specify it as UTC
        parameter.Value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public override DateTime Parse(object value)
    {
        // When reading from the database, convert to DateTime and force UTC Kind
        if (value == null || value is DBNull)
        {
            return default;
        }

        string dateString = value.ToString()!;

        //Normally DateTime.Parse will convert to localtime
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsedDate))
        {
            return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
        }
        throw new FormatException($"Unable to parse value from database,'{dateString}', into a UTC DateTime.");
    }
}
