
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Scaffolding.NET.Helper;

public static class MinecraftProtocolHelper
{
    private static UdpClient? _client;
    private static UdpClient? _clientV6;
    
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.2.60");
    private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse("ff75:230::60");
    
    private static readonly Regex RegexPort = new Regex(@"\[AD\](.*?)\[/AD\]");

    static MinecraftProtocolHelper() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();

    private static void Cleanup()
    {
        _client?.Dispose();
        _clientV6?.Dispose();
    }
    
    private static void InitializeUdpClient()
    {
        if (_client is not null || _clientV6 is not null) return;
        
        // IPv4
        _client = new UdpClient();
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, 4445));
        
        // IPv6
        _clientV6 = new UdpClient(AddressFamily.InterNetworkV6);
        _clientV6.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _clientV6.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, 4445));
    }
    
    public static Task<ushort> GetMinecraftServerPortAsync() => GetMinecraftServerPortAsync(CancellationToken.None);

    public static async Task<ushort> GetMinecraftServerPortAsync(CancellationToken token)
    {
        InitializeUdpClient();
        
        _client!.JoinMulticastGroup(MulticastAddress);
        _clientV6!.JoinMulticastGroup(MulticastAddressV6);

        var v4Task = ListeningTask(_client, MulticastAddress, token);
        var v6Task = ListeningTask(_clientV6, MulticastAddressV6, token);

        var completedTask = await Task.WhenAny(v4Task, v6Task);
        return await completedTask;
    }

    private static async Task<ushort> ListeningTask(UdpClient client, IPAddress group, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync();
                var sourceString = Encoding.UTF8.GetString(result.Buffer);
                var match = RegexPort.Match(sourceString);
                
                if (!match.Success || !ushort.TryParse(match.Groups[1].Value.Trim(), out var port))
                {
                    return 0;
                }

                return port;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        
        client.DropMulticastGroup(group);

        return 0;
    }
}