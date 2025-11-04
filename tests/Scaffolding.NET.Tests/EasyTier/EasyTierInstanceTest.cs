using JetBrains.Annotations;
using Scaffolding.NET.EasyTier;

namespace Scaffolding.NET.Tests.EasyTier;

[TestSubject(typeof(EasyTierInstance))]
public class EasyTierInstanceTest(ITestOutputHelper output)
{

    [Fact]
    public async Task TestGetEasyTierNodesAsync()
    {
        var result = await EasyTierInstance.GetEasyTierNodesAsync();
        foreach (var item in result)
        {
            output.WriteLine(item);
        }
    }
}