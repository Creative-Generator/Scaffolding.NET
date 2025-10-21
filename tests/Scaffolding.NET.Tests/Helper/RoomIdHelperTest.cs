using JetBrains.Annotations;
using Scaffolding.NET.Helper;

namespace Scaffolding.NET.Tests.Helper;

[TestSubject(typeof(RoomIdHelper))]
public class RoomIdHelperTest
{

    [Theory]
    [InlineData("U/WJAJ-47A0-KXTU-SEUQ", true)]
    [InlineData("U/Z95T-XBYT-5EWV-JFQ4", true)]
    [InlineData("U/U94T-A1Y8-D07R-KKLP", true)]
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
        Assert.True(RoomIdHelper.IsValidRoomId(RoomIdHelper.GenerateRoomId()));
    } 
}