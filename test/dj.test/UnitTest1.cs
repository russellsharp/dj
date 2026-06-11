using System.Diagnostics;
using Xunit.Abstractions;
using FluentAssertions;

namespace dj;

public class UnitTest1(ITestOutputHelper _output)
{
    [Fact]
    public void Test1()
    {
        _output.WriteLine("hello");
        Assert.False(true);
    }
}
