using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Scaffolding.NET.Extensions.Minecraft;

public static class MinecraftProtocolHelper
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.2.60");
    private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse("ff75:230::60");
    
    private static readonly Regex RegexPort = new Regex(@"\[AD\](.*?)\[/AD\]");
    
    private static UdpClient CreateUdpClient(AddressFamily family)
    {
        var client = new UdpClient(family);
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        var endPoint = family == AddressFamily.InterNetwork
            ? new IPEndPoint(IPAddress.Any, 4445)
            : new IPEndPoint(IPAddress.IPv6Any, 4445);
        client.Client.Bind(endPoint);
        return client;
    }

    public static Task<ushort> GetMinecraftServerPortAsync() => GetMinecraftServerPortAsync(CancellationToken.None);

    public static async Task<ushort> GetMinecraftServerPortAsync(CancellationToken token)
    {
        using var clientV4 = CreateUdpClient(AddressFamily.InterNetwork);
        using var clientV6 = CreateUdpClient(AddressFamily.InterNetworkV6);

        clientV4.JoinMulticastGroup(MulticastAddress);
        clientV6.JoinMulticastGroup(MulticastAddressV6);

        var v4Task = ListeningTask(clientV4, MulticastAddress, token);
        var v6Task = ListeningTask(clientV6, MulticastAddressV6, token);

        var completedTask = await Task.WhenAny(v4Task, v6Task);
        return await completedTask;
    }

    private static async Task<ushort> ListeningTask(UdpClient client, IPAddress group, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(
                    #if NET6_0_OR_GREATER
                    token
                    #endif
                    );
                var sourceString = Encoding.UTF8.GetString(result.Buffer);
                var match = RegexPort.Match(sourceString);

                if (!match.Success || !ushort.TryParse(match.Groups[1].Value.Trim(), out var port))
                {
                    continue;
                }
                
                return port;
            }
            catch (OperationCanceledException)
            {
                client.DropMulticastGroup(group);
                break;
            }
            finally
            {
                client.DropMulticastGroup(group);
            }
        }
        
        client.DropMulticastGroup(group);

        return 0;
    }
}
