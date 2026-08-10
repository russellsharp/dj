using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace shared.http.security;

public class SecurityConfiguration
{
    private static string SecurityKeyKey = "DJ_SECURITY_KEY";
    private string _securityKey = "";
    public string SecurityKey
    {
        get
        {
            _securityKey = Environment.GetEnvironmentVariable(SecurityKeyKey);
            if (string.IsNullOrWhiteSpace(_securityKey))
            {
                throw new InvalidOperationException(
                    $"Required environment variable '{SecurityKeyKey}' was not set or was empty.");
            }
            return _securityKey;
        }
    }
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

    public static IHostApplicationBuilder AddSecurityConfiguration(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<SecurityConfiguration>(options => { });

        builder.Services.Configure<OpenIdDictDatabaseConfiguration>(builder.Configuration.GetSection(OpenIdDictDatabaseConfiguration.SectionName));

        return builder;
    }
}
