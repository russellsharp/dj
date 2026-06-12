using System.Diagnostics;
using Xunit.Abstractions;
using FluentAssertions;
using shared;
namespace dj;

public class FileResultHelperTests(ITestOutputHelper _output)
{
    [Fact]
    public void Available()
    {
        FileAccessResult result = FileAccessResult.Available;
        var path = @"c:/media/metal/AngelOfDeath.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File is available for access: {FileAccess.Read}, " + path);
    }

    [Fact]
    public void NoAccess()
    {
        FileAccessResult result = FileAccessResult.UnauthorizedAccessException;
        var path = @"c:/media/metal/AngelOfDeath.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File cannot be accessed with requested access: {FileAccess.Read}, " + path);
    }

    [Fact]
    public void DoesNotExist()
    {
        FileAccessResult result = FileAccessResult.DoesNotExist;
        var path = @$"c:/media/metal/{Guid.NewGuid}.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File does not exist: {FileAccess.Read}, " + path);
    }


    [Fact]
    public void Locked()
    {
        FileAccessResult result = FileAccessResult.Locked;
        var path = @"c:/media/metal/AngelOfDeath.mp3";
        var message = result.AccessMessage(path, FileAccess.Read);
        message.Should().Be($"File is locked by another process: {FileAccess.Read}, " + path);
    }
}
