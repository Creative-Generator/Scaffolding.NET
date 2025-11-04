using JetBrains.Annotations;

namespace Scaffolding.NET.Extensions.MachineId.Tests;

[TestSubject(typeof(MachineIdHelper))]
public class MachineIdHelperTest(ITestOutputHelper output)
{

    [Fact]
    public async Task TestMachineId()
    {
        output.WriteLine(await MachineIdHelper.GetMachineIdAsync());
    }
}