using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace shared.http.security;

public class SecurityConfiguration
{
    public string SecurityKey { get; set; }
}

public static class SecurityExtensions
{
    public static string SecurityKeyKey = "DJ_SECURITY_KEY";

    public static IHostApplicationBuilder AddSecurityConfiguration(this IHostApplicationBuilder builder)
    {
        var securityKey = Environment.GetEnvironmentVariable(SecurityKeyKey);
        if (string.IsNullOrWhiteSpace(securityKey))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{SecurityKeyKey}' was not set or was empty.");
        }

        builder.Services.Configure<SecurityConfiguration>(options => { options.SecurityKey = securityKey; });

        return builder;
    }
}
