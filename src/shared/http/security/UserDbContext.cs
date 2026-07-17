using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace shared.http.security;

public class RegisteredScopes : List<Scopes>;
public class UserInformation
{
    [Key]
    public string ClientId { get; set; } = "";

    [JsonConverter(typeof(ScopeListConverter))]
    public RegisteredScopes GrantedScopes { get; set; } = new();
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
};

public class ScopeEntry
{
    [Key]
    public Scopes Value;
}

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }
    public DbSet<UserInformation> UserInfo { get; set; }
    public DbSet<ScopeEntry> ApplicationScopes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Define the Primary Key
        modelBuilder.Entity<UserInformation>()
            .HasKey(u => u.ClientId);

        modelBuilder.Entity<ScopeEntry>()
            .HasKey(u => u.Value);

        // Tell InMemory how to store the List<RegisteredScopes> as a JSON string internally
        modelBuilder.Entity<UserInformation>()
            .Property(u => u.GrantedScopes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<RegisteredScopes>(v, (JsonSerializerOptions)null) ?? new RegisteredScopes()
            );
    }
}

public static class UserDBAppExtensions
{
    public static WebApplication SetupTestData(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var userContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            userContext.Database.EnsureCreated();

            //seed userContext with test users
            if (!userContext.UserInfo.Any())
            {
                userContext.UserInfo.Add(new UserInformation
                {
                    ClientId = "console-app-client-read",
                    GrantedScopes = [Scopes.MediaRead],
                    DisplayName = "console-app-client-read",
                    Password = "super-secret-password-123"
                });

                userContext.UserInfo.Add(new UserInformation
                {
                    ClientId = "console-app-client-rw",
                    GrantedScopes = [Scopes.MediaWrite, Scopes.MediaRead],
                    DisplayName = "console-app-client-rw",
                    Password = "super-secret-password-123"
                });

            }

            if (!userContext.ApplicationScopes.Any())
            {
                userContext.ApplicationScopes.AddRange(
                    [
                        new ScopeEntry { Value = Scopes.MediaRead },
                        new ScopeEntry { Value = Scopes.MediaWrite }
                    ]);
            }

            userContext.SaveChanges();
        }
        return app;
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