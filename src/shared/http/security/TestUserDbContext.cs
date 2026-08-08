using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace shared.http.security;

public class RegisteredScopes : List<Scopes>;

public class UserInformation
{
    [Key]
    public string client_id { get; set; } = "";

    [JsonConverter(typeof(ScopeListConverter))]
    public RegisteredScopes scopes { get; set; } = [];
    public string password_plaintext { get; set; } = "";
    public string display_name { get; set; } = "";
    public DateTime created_at { get; set; } = DateTime.UtcNow;
};

public class ScopeEntry
{
    [Key]
    public Scopes Value;
}

public class TestUserDbConfiguration : BaseDatabaseConfiguration
{
    public new static string SectionName = nameof(TestUserDbConfiguration);
    protected override string _dbFIlePath { get; set; } = "data/testuser.db";
}

public class TestUserDbContext : DbContext
{
    public TestUserDbContext(DbContextOptions<TestUserDbContext> options) : base(options) { }
    public DbSet<UserInformation> UserInfo { get; set; }
    public DbSet<ScopeEntry> ApplicationScopes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Define the Primary Key
        modelBuilder.Entity<UserInformation>()
            .HasKey(u => u.client_id);

        modelBuilder.Entity<ScopeEntry>()
            .HasKey(u => u.Value);

        // Tell InMemory how to store the List<RegisteredScopes> as a JSON string internally
        modelBuilder.Entity<UserInformation>()
            .Property(u => u.scopes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<RegisteredScopes>(v, (JsonSerializerOptions)null) ?? new RegisteredScopes()
            );
    }
}

public class ScopeListConverter : JsonConverter<RegisteredScopes>
{
    public override RegisteredScopes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected start of a JSON array.");
        }

        var scopes = new RegisteredScopes();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            string? enumString = reader.GetString();
            if (Enum.TryParse<Scopes>(enumString, ignoreCase: true, out var enumValue))
            {
                scopes.Add(enumValue);
            }
            else
            {
                throw new JsonException($"Unknown scope during deserialization: {enumString}.");
            }
        }
        return scopes;

    }

    public override void Write(Utf8JsonWriter writer, RegisteredScopes value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var scope in value)
        {
            writer.WriteStringValue(scope.ToString().ToLower());
        }

        writer.WriteEndArray();
    }
}