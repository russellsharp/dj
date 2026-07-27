using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace shared.http.security;

public class SecurityConfiguration
{
    public string SecurityKey { get; set; }
}

public record AwsSecret
{
    public string ARN { get; init; }
    public string Name { get; init; }
    public string VersionId { get; init; }
    public SecretString SecretString { get; init; }
    public List<string> VersionStages { get; init; }
    public DateTime CreatedDate { get; init; }
}

public record SecretString(string name, string value);

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
