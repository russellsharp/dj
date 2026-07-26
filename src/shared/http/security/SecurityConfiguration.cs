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
        string securityKey = Environment.GetEnvironmentVariable(SecurityKeyKey) ?? "";

        ArgumentException.ThrowIfNullOrEmpty(securityKey, $"{securityKey.Take(5)} -- {securityKey.TakeLast(5)}");

        Console.WriteLine($"{securityKey.Take(5)} -- {securityKey.TakeLast(5)}");
        Debug.WriteLine($"{securityKey.Take(5)} -- {securityKey.TakeLast(5)}");

        var config = new SecurityConfiguration { SecurityKey = securityKey };

        builder.Services.Configure<SecurityConfiguration>(options => { options.SecurityKey = securityKey; });

        return builder;
    }
}
