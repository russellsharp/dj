using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace shared.http.security;

public class RegisteredScopes : List<Scopes>;

public class UserInformation
{
    [Key]
    public string client_id { get; set; } = "";

    [JsonConverter(typeof(ScopeListConverter))]
    public RegisteredScopes scopes { get; set; } = [];
    public string password_hash { get; set; } = "";
    public string display_name { get; set; } = "";
    public DateTime created_at { get; set; } = DateTime.UtcNow;
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

public static class UserDBAppExtensions
{

    public static IHostApplicationBuilder AddUserAuthDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IUserDatabase, UserDatabase>();

        // builder.Services.AddDbContext<UserDbContext>(options => options.UseSqlServer(UserDatabase.ConnectionString));

        // builder.Services.Configure<IDatabaseConfiguration>(builder.Configuration.GetSection(UserDatabase.SectionName));
        builder.Services.AddDbContext<UserDbContext>(options => options.UseInMemoryDatabase("UserDatabase"));

        return builder;
    }

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
                    client_id = "console-app-client-read",
                    scopes = [Scopes.MediaRead],
                    display_name = "console-app-client-read",
                    password_hash = "super-secret-password-123"
                });

                userContext.UserInfo.Add(new UserInformation
                {
                    client_id = "console-app-client-rw",
                    scopes = [Scopes.MediaWrite, Scopes.MediaRead],
                    display_name = "console-app-client-rw",
                    password_hash = "super-secret-password-123"
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