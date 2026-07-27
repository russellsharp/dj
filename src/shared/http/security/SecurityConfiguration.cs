using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
        string securityKey = string.Empty;
        var envValue = Environment.GetEnvironmentVariable(SecurityKeyKey) ?? string.Empty;

        if (!string.IsNullOrEmpty(envValue))
        {
            try
            {
                // Try to parse as an AWS Secrets Manager JSON response first
                var fullbody = System.Text.Json.JsonSerializer.Deserialize<AwsSecret>(envValue);
                securityKey = fullbody?.SecretString?.value ?? envValue;
            }
            catch (System.Text.Json.JsonException)
            {
                // Not an AWS Secret JSON format — use the raw value as the key
                securityKey = envValue;
            }
        }

        if (!string.IsNullOrEmpty(securityKey))
        {
            Console.WriteLine("Security key loaded from environment variable.");
        }

        builder.Services.Configure<SecurityConfiguration>(options => { options.SecurityKey = securityKey; });

        return builder;
    }
}
