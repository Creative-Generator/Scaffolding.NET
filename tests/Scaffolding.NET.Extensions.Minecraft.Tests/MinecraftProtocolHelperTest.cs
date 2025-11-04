using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Scaffolding.NET.Extensions.Minecraft.Tests;

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

    [Fact]
    public async Task TestMultiGetMinecraftServerPortAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            var result = await MinecraftProtocolHelper.GetMinecraftServerPortAsync(TestContext.Current.CancellationToken);
            output.WriteLine(result.ToString());
            Assert.NotEqual(0, result);
        }
    }
}