using Microsoft.EntityFrameworkCore;

namespace shared.http.security;

public class OpenIdDictDatabaseContext : DbContext
{
    public OpenIdDictDatabaseContext(DbContextOptions<OpenIdDictDatabaseContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // This registers the OpenIddict tables in your SQLite schema
        modelBuilder.UseOpenIddict();
    }
}