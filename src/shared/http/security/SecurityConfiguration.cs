using System;
using System.Collections.Generic;
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

public static class SecurityExtensions
{
    public static string SecurityKeyKey = "DJ_SECURITY_KEY";

    public static IHostApplicationBuilder AddSecurityConfiguration(this IHostApplicationBuilder builder)
    {
        string securityKey = Environment.GetEnvironmentVariable(SecurityKeyKey) ?? "";

        var config = new SecurityConfiguration { SecurityKey = securityKey };

        builder.Services.Configure<SecurityConfiguration>(options => { options.SecurityKey = securityKey; });

        return builder;
    }
}