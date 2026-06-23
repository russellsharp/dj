using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Globalization;
using Dapper;

namespace shared.data;

[Table("file")]
public record File
{
    public string? path_hash;
    public required string path;
    public required DateTime date_modified;
    public required DateTime date_created;
    public required long size;
    public required string extension;
    public string? hash;
    public string? attributes;
    public string? extra_attributes;
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
