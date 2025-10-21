using JetBrains.Annotations;
using Scaffolding.NET.Helper;

namespace Scaffolding.NET.Tests.Helper;

[TestSubject(typeof(MinecraftProtocolHelper))]
public class MinecraftProtocolHelperTest(ITestOutputHelper output)
{
    [Fact]
    public async Task TestGetMinecraftServerPortAsync()
    {
        var result = await MinecraftProtocolHelper.GetMinecraftServerPortAsync(TestContext.Current.CancellationToken);
        output.WriteLine(result.ToString());
        Assert.NotEqual(0, result);
    }
}