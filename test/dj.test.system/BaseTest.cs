using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace dj.test.system;

public class BaseTest(ITestOutputHelper _log)
{
    public const string BaseUrl = "https://localhost:7123/api";

    public void Log(object msg)
    {
        var message = Convert.ToString(msg);
        Debug.WriteLine(message);
        Console.WriteLine(message);
        _log.WriteLine(message);
    }
}
