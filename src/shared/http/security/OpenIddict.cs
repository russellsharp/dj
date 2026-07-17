using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Polly;

namespace shared.http.security;

public class UserDbContext : DbContext
{
    public class UserInformation
    {
        [Key]
        public string ClientId { get; set; } = "";
        public List<string> Scopes { get; set; } = new();
        public string Password { get; set; } = "";
        public string DisplayName { get; set; } = "";
    };

    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<UserInformation> UserInfo { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Define the Primary Key
        modelBuilder.Entity<UserInformation>()
            .HasKey(u => u.ClientId);

        // Tell InMemory how to store the List<string> as a JSON string internally
        modelBuilder.Entity<UserInformation>()
            .Property(u => u.Scopes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null) ?? new List<string>()
            );

    }
}

public static partial class ApplicationExtensions
{
    public static IHostApplicationBuilder AddOpenIddict(this IHostApplicationBuilder builder)
    {

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ReadScope", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c =>
                        c.Type == "scope" &&
                        c.Value.Split(' ').Contains("items:read")
                    )
                ));

            options.AddPolicy("WriteScope", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c =>
                        c.Type == "scope" &&
                        c.Value.Split(' ').Contains("items:write")
                    )
                ));
        });

        builder.Services.AddOpenIddictService();

        return builder;
    }

    private static IServiceCollection AddOpenIddictService(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase("OAuthDatabase");
            options.UseOpenIddict();
        });

        services.AddDbContext<UserDbContext>(options => options.UseInMemoryDatabase("UserDatabase"));

        services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>();
                })
                .AddServer(options =>
                {
                    // Register the custom scopes so OpenIddict accepts them in token requests.
                    options.RegisterScopes(["items:read", "items:write"]);

                    // Disable builtin scope validation so our controller can accept and validate custom scopes.
                    options.DisableScopeValidation();

                    // Enable the token endpoint (where clients request tokens)
                    options.SetTokenEndpointUris("/api/token/scoped");
                    options.AllowClientCredentialsFlow(); // Machine-to-machine exchange

                    // ADD THIS LINE to downgrade encryption to visible JWT format
                    options.DisableAccessTokenEncryption();

                    // Register the cryptographic signing keys
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();

                    // Register the ASP.NET Core host
                    options.UseAspNetCore().EnableTokenEndpointPassthrough();
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
        return services;
    }

    public static async Task<WebApplication> SetupTestClient(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var userContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            userContext.Database.EnsureCreated();

            //seed userContext with test users
            if (!userContext.UserInfo.Any())
            {
                userContext.UserInfo.Add(new UserDbContext.UserInformation
                {
                    ClientId = "console-app-client-read",
                    Scopes = ["items:read"],
                    DisplayName = "console-app-client-read",
                    Password = "super-secret-password-123"
                });

                userContext.SaveChanges();
            }

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            Console.WriteLine("wooooo");
            Debug.WriteLine("ahhhh");

            foreach (var user in userContext.UserInfo)
            {
                Console.WriteLine("asdf;asldkfj;asdlkfja;sdlkfj " + user.ClientId);
                Debug.WriteLine("asdf;asldkfj;asdlkfja;sdlkfj " + user.ClientId);
                // Check if our test client already exists
                var application = await manager.FindByClientIdAsync(user.ClientId);

                if (application == null)
                {
                    var permissions = user.Scopes.ToHashSet();

                    var applicationDescriptor = new OpenIddictApplicationDescriptor
                    {
                        ClientId = user.ClientId,
                        ClientSecret = user.Password,
                        DisplayName = user.DisplayName,
                        Permissions =
                                    {                                        
                                        // Must explicitly permit the flow and endpoint
                                        OpenIddictConstants.Permissions.Endpoints.Token,
                                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                                        "items:read"
                                    }
                    };
                    var result = await manager.CreateAsync(applicationDescriptor);

                    application = await manager.FindByClientIdAsync(user.ClientId);
                    if (application is null)
                    {
                        throw new Exception("Application not found.");
                    }
                    await manager.PopulateAsync(applicationDescriptor, application);

                    foreach (var grantingScope in user.Scopes)
                    {
                        var permission = $"scp:{grantingScope}";
                        applicationDescriptor.Permissions.Add(permission);
                    }

                    await manager.UpdateAsync(application, applicationDescriptor);
                }
            }
        }

        return app;
    }
}