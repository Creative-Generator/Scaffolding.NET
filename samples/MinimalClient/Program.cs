using Scaffolding.NET;
using Scaffolding.NET.EasyTier;
using Scaffolding.NET.Extensions.MachineId;

namespace MinimalClient;

class Program
{
    static async Task Main(string[] args)
    {
        // 房间码
        var roomId = "";
        var playerName = "ScaffoldingNET_Test";
        
        var etFileInfo = new EasyTierFileInfo
        {
            EasyTierFolderPath = @"C:\Dev\EasyTier"
        };
        
        var options = new ScaffoldingClientOptions
        {
            PlayerName = playerName,
            MachineId = await MachineIdHelper.GetMachineIdAsync(),
            EasyTierFileInfo = etFileInfo,
            EasyTierStartInfo = new EasyTierStartInfo
            {
                Dhcp = true
            },
            RoomId = roomId
        };
        
        var client = await ScaffoldingClient.ConnectAsync(options);

        while (true)
        {
            await Task.Delay(5000);
            
            Console.WriteLine($"房主IP:{client.RoomInfo.Host.Ip!.ToString()}");
            Console.WriteLine($"端口:{client.RoomInfo.ServerPort}");
        }
    }
}