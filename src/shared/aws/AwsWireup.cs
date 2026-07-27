using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace shared.aws;

public static partial class ApplicationExtensions
{
    public static IHostApplicationBuilder ConfigureAws(this IHostApplicationBuilder builder)
    {

        return builder;
    }

    public static IHostApplicationBuilder SetupAws(this IHostApplicationBuilder builder)
    {

        return builder;
    }
}
