using System.Net;
using System.Net.Sockets;

namespace Scaffolding.NET.Helpers;

public static class NetworkHelper
{
    public static ushort GetTcpAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return (ushort)port;
    }

    public static ushort GetUdpAvailablePort()
    {
        using var client = new UdpClient();
        client.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = (IPEndPoint)client.Client.LocalEndPoint!;
        return (ushort)port.Port;
    }
}