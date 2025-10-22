using JetBrains.Annotations;
using Scaffolding.NET.Extensions.MachineId;

namespace Scaffolding.NET.Extensions.MachineId.Tests;

[TestSubject(typeof(MachineIdHelper))]
public class MachineIdHelperTest(ITestOutputHelper output)
{

    [Fact]
    public void TestMachineId()
    {
        output.WriteLine(MachineIdHelper.GetMachineId());
    }
}