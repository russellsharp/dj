using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Polly;
using shared.util;

namespace shared.http.security;

public enum Scopes
{
    [Description("media:read")]
    MediaRead,
    [Description("media:write")]
    MediaWrite
}

public static class ScopesExtensions
{
    public static string ToOidc(this Scopes value)
    {
        return value.ToDescription();
    }
}

public static partial class ApplicationExtensions
{
    public static IHostApplicationBuilder AddOpenIddict(this IHostApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new ScopeListConverter());
        });
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new ScopeListConverter());
        });
        builder.Services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new ScopeListConverter());
        });

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
                        c.Value.Split(' ').Contains(Scopes.MediaRead.ToOidc())
                    )
                ));

            options.AddPolicy("WriteScope", policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c =>
                        c.Type == "scope" &&
                        c.Value.Split(' ').Contains(Scopes.MediaWrite.ToOidc())
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
                    options.RegisterScopes(["media:read", "media:write"]);

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

            app.SetupTestData();

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            foreach (var user in userContext.UserInfo)
            {
                // Check if our test client already exists
                var application = await manager.FindByClientIdAsync(user.ClientId);

                if (application is null)
                {
                    var applicationDescriptor = new OpenIddictApplicationDescriptor
                    {
                        ClientId = user.ClientId,
                        ClientSecret = user.Password,
                        DisplayName = user.DisplayName,
                        Permissions =
                                    {                                        
                                        // Must explicitly permit the flow and endpoint
                                        OpenIddictConstants.Permissions.Endpoints.Token,
                                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials
                                    }
                    };

                    var result = await manager.CreateAsync(applicationDescriptor);

                    application = await manager.FindByClientIdAsync(user.ClientId) ?? throw new Exception("Application not found.");

                    await manager.PopulateAsync(applicationDescriptor, application);

                    foreach (var grantingScope in user.GrantedScopes)
                    {
                        var permission = $"scp:{grantingScope.ToOidc()}";
                        applicationDescriptor.Permissions.Add(permission);
                    }

                    await manager.UpdateAsync(application, applicationDescriptor);
                }
            }
        }

        return app;
    }
}