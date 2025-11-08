using JetBrains.Annotations;
using Scaffolding.NET.Room;

namespace Scaffolding.NET.Tests.Helpers;

[TestSubject(typeof(RoomIdHelper))]
public class RoomIdHelperTest(ITestOutputHelper output)
{

    [Theory]
    [InlineData("U/WJAJ-47A0-KXTU-SEUQ", true)]
    [InlineData("U/Z95T-XBYT-5EWV-JFQ4", true)]
    [InlineData("U/U94T-A1Y8-D07R-KKLP", true)]
    [InlineData("U/A19X-PEG6-JQTP-9LHD", false)]
    [InlineData("U/ABCD-ABCD-ABCD-ABCD", false)]
    [InlineData("qwertyqwerty" ,false)]
    [InlineData("", false)]
    public void TestIsValidRoomId(string input, bool expected)
    {
        var result = RoomIdHelper.IsValidRoomId(input);
        Assert.Equal(result, expected);
    }
    
    [Fact]
    public void TestGenerateRoomId()
    {
        var roomId = RoomIdHelper.GenerateRoomId();
        output.WriteLine(roomId);
        Assert.True(RoomIdHelper.IsValidRoomId(roomId));
    } 
}