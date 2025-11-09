using Scaffolding.NET;
using Scaffolding.NET.EasyTier;
using Scaffolding.NET.Extensions.MachineId;
using Scaffolding.NET.Packet;

namespace MinimalClient;

class Program
{
    static async Task Main(string[] args)
    {
        var roomId = "";
        var playerName = "ScaffoldingNET_Test";
        var etFolderPath = @"C:\Dev\EasyTier";
        
        
        var options = new ScaffoldingClientOptions
        {
            PlayerName = playerName,
            MachineId = await MachineIdHelper.GetMachineIdAsync(),
            EasyTierFileInfo = new EasyTierFileInfo
            {
                EasyTierFolderPath = etFolderPath
            },
            EasyTierStartInfo = new EasyTierStartInfo
            {
                Dhcp = true
            },
            RoomId = roomId
        };
        
        var client = await ScaffoldingClient.ConnectAsync(options);

        while (true)
        {
            
            Console.WriteLine("玩家信息:");
            foreach (var playerInfo in client.RoomInfo.PlayerInfos)
            {
                Console.WriteLine($"名字:{playerInfo.Name}");
                Console.WriteLine($"软件:{playerInfo.Vendor}");
                Console.WriteLine($"机器码:{playerInfo.MachineId}");
                Console.WriteLine($"类型:{playerInfo.Kind}");
                Console.WriteLine("");
            }
            
            Console.WriteLine($"端口:{client.RoomInfo.Port}");

            await Task.Delay(5000);
        }
    }
}
