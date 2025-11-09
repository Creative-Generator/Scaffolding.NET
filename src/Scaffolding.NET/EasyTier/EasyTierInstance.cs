using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scaffolding.NET.Helpers;

namespace Scaffolding.NET.EasyTier;

internal sealed class EasyTierInstance
{
    private readonly EasyTierFileInfo _fileInfo;
    private readonly Process? _process;
    private readonly int _rpcPort;
    
    public bool IsRunning => _process?.HasExited == false;

    public event EventHandler? Exited;

    public EasyTierInstance(EasyTierFileInfo fileInfo)
    {
        if (!fileInfo.CheckEasyTierEnvironment())
        {
            throw new ArgumentException("不是合格的 EasyTier 环境。");
        }

        _fileInfo = fileInfo;

        _process = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(_fileInfo.EasyTierFolderPath, _fileInfo.EasyTierCoreName),
                WorkingDirectory = _fileInfo.EasyTierFolderPath,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _rpcPort = GetTcpAvailablePort();
    }

    public async Task<bool> StartAsync(EasyTierStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        _process!.StartInfo.Arguments = await BuildArgumentsAsync(startInfo, cancellationToken);

        _process.Exited += Exited;
        
        return _process.Start();
    }

    public void Stop()
    {
        _process!.Kill();
    }

    private async Task<string> BuildArgumentsAsync(EasyTierStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        if (startInfo.NetworkName is null) throw new ArgumentNullException(nameof(startInfo.NetworkName));
        if (startInfo.NetworkSecret is null) throw new ArgumentNullException(nameof(startInfo.NetworkSecret));
        if (startInfo.Ipv4Address is null && !startInfo.Dhcp)
            throw new InvalidCastException("在未设置 DHCP 情况下必须设置 IPv4 地址。");

        var argsBuilder = new ArgumentsBuilder();

        argsBuilder.AddFlag("no-tun")
            .AddFlag("multi-thread")
            .AddFlag("enable-kcp-proxy")
            .AddFlag("enable-quic-proxy")
            .AddFlagIf(!startInfo.TryPunchSym, "disable-sys-hole-punching")
            .AddFlagIf(!startInfo.EnableIPv6, "disable-ipv6")
            .AddFlagIf(startInfo.LatencyFirstMode, "latency-first")
            .Add("encryption-algorithm", "aes-gcm")
            .Add("compression", "zstd")
            .Add("default-protocol", startInfo.DefaultProtocol.ToString().ToLowerInvariant())
            .Add("network-name", startInfo.NetworkName)
            .Add("network-secret", startInfo.NetworkSecret)
            //.Add("relay-network-whitelist", startInfo.NetworkName)
            .AddIf(startInfo.MachineId is not null, "machine-id", startInfo.MachineId!)
            .Add("rpc-portal", _rpcPort.ToString())
            .Add("hostname", startInfo.HostName)
            .AddFlagIf(startInfo.Ipv4Address is null || startInfo.Dhcp, "dhcp")
            .Add("l", "tcp://0.0.0.0:0")
            .Add("l", "udp://0.0.0.0:0")
            .AddIf(!startInfo.RelayForOthers, "private-mode", "true")
            .AddIf(startInfo.DisableP2P, "disable-p2p", "true")
            .Add("private-mode", "true");

        if (startInfo.Ipv4Address is not null)
        {
            argsBuilder.Add("ipv4", startInfo.Ipv4Address.ToString());
        }
        
        if (startInfo.TcpWhiteList is not null)
        {
            foreach (var whitelist in startInfo.TcpWhiteList)
            {
                argsBuilder.Add("tcp-whitelist", whitelist);
            }
        }
        else
        {
            argsBuilder.Add("tcp-whitelist", "0");
        }

        if (startInfo.UdpWhiteList is not null)
        {
            foreach (var whitelist in startInfo.UdpWhiteList)
            {
                argsBuilder.Add("udp-whitelist", whitelist);
            }
        }
        else
        {
            argsBuilder.Add("udp-whitelist", "0");
        }

        if (startInfo.RelayServers is not null)
        {
            foreach (var node in startInfo.RelayServers)
            {
                argsBuilder.Add("peers", node);
            }
        }

        var nodes = await GetEasyTierNodesAsync(cancellationToken);
        foreach (var node in nodes)
        {
            argsBuilder.Add("peers", node);
        }

        var result = argsBuilder.GetResult();
        DebugHelper.WriteLine($"EasyTier 参数: {result}");
        return result;
    }

    internal static async Task<List<string>> GetEasyTierNodesAsync(CancellationToken cancellationToken = default)
    {
        List<string> officialNodes = ["tcp://public.easytier.cn:11010", "tcp://public2.easytier.cn:54321"];
        var nodes = new List<string>(7);
        nodes.AddRange(officialNodes);
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.BaseAddress = new Uri("https://uptime.easytier.cn");

            using var response =
                await httpClient.GetAsync("api/nodes?page=1&per_page=10000&is_active=true", cancellationToken);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            var success = root.GetProperty("success").GetBoolean();
            if (!success) return officialNodes;

            var items = root.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();

            var rand = new Random();
            var total = root.GetProperty("data").GetProperty("total").GetInt32();

            for (var i = 0; i < 5; i++)
            {
                var index = rand.Next(0, total);
                var protocol = items[index].GetProperty("protocol").GetString();
                var host = items[index].GetProperty("host").GetString();
                var port = items[index].GetProperty("port").GetString();

                nodes.Add($"{protocol}://{host}:{port}");
            }
        }
        catch (Exception)
        {
            DebugHelper.WriteLine("调用 EasyTier API 失败，使用官方节点");
        }

        return nodes.Distinct().ToList();
    }

    public async Task<List<EasyTierPeerInfo>> GetEasyTierPeerInfosAsync()
    {
        var json = await CallEasyTierCliAsync("-o json peer");

#if NET6_0_OR_GREATER
        return JsonSerializer.Deserialize(json!, EasyTierPeerInfoContext.Default.ListEasyTierPeerInfo)!;
#else
        return JsonSerializer.Deserialize<List<EasyTierPeerInfo>>(json!)!;
#endif
    }

    internal async Task<ushort> SetPortForwardAsync(IPAddress targetIp, ushort targetPort)
    {
        var localPort = GetTcpAvailablePort();

        await CallEasyTierCliAsync($"port-forward add tcp 127.0.0.1:{localPort} {targetIp}:{targetPort}", false);
        await CallEasyTierCliAsync($"port-forward add udp 127.0.0.1:{localPort} {targetIp}:{targetPort}", false);
        await CallEasyTierCliAsync($"port-forward add tcp [::]:{localPort} {targetIp}:{targetPort}", false);
        await CallEasyTierCliAsync($"port-forward add udp [::]:{localPort} {targetIp}:{targetPort}", false);

        return localPort;
    }

    public async Task<string?> CallEasyTierCliAsync(string arguments, bool getOutput = true)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(_fileInfo.EasyTierFolderPath, _fileInfo.EasyTierCliName),
            WorkingDirectory = _fileInfo.EasyTierFolderPath,
            Arguments = $"--rpc-portal 127.0.0.1:{_rpcPort} {arguments}",
            ErrorDialog = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
#if NET6_0_OR_GREATER
            StandardInputEncoding = Encoding.UTF8
#endif
        };
        process.EnableRaisingEvents = true;

#if NET6_0_OR_GREATER
#else
        var tcs = new TaskCompletionSource<object?>();
        process.Exited += (_, _) => tcs.TrySetResult(null);
#endif

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false) +
                           await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        
#if NET6_0_OR_GREATER
        await process.WaitForExitAsync().ConfigureAwait(false);
#else
        await tcs.Task.ConfigureAwait(false);
#endif
        
        return getOutput ? output : null;
    }

    private static ushort GetTcpAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return (ushort)port;
    }

    private static ushort GetUdpAvailablePort()
    {
        using var client = new UdpClient();
        client.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = (IPEndPoint)client.Client.LocalEndPoint!;
        return (ushort)port.Port;
    }
}

#if NET6_0_OR_GREATER
[JsonSerializable(typeof(List<EasyTierPeerInfo>))]
public partial class EasyTierPeerInfoContext : JsonSerializerContext;
#endif