using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Scaffolding.NET.EasyTier;
using Scaffolding.NET.Helpers;
using Scaffolding.NET.Packet;
using Scaffolding.NET.Room;

namespace Scaffolding.NET;

public sealed class ScaffoldingClient : IAsyncDisposable
{
    private CancellationTokenSource? _pipeCts;
    private CancellationTokenSource? _heartbeatCts;
    private bool _isDisposed;
    private readonly TcpClient _tcpClient;
    private readonly EasyTierInstance _easyTierInstance;
    private readonly Pipe _pipe;
    private readonly Channel<ScaffoldingResponsePacket> _responseChannel;
    private readonly Stopwatch _heartbeatStopwatch = new();
    private readonly ScaffoldingRequestPacket _playerPingPacket;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string SupportedProtocols =
        "c:ping\0c:protocols\0c:server_port\0c:player_ping\0c:player_profiles_list\0c:player_easytier_id";

    public ScaffoldingClientOptions Options { get; init; }
    public ScaffoldingRoomInfo RoomInfo { get; private set; }

    private ScaffoldingClient(ScaffoldingClientOptions options)
    {
        Options = options;

        _tcpClient = new TcpClient();
        _easyTierInstance = new EasyTierInstance(options.EasyTierFileInfo);
        _responseChannel = Channel.CreateUnbounded<ScaffoldingResponsePacket>();
        _pipe = new Pipe();
        RoomInfo = new ScaffoldingRoomInfo();

        var json = new JsonObject
        {
            ["name"] = options.PlayerName,
            ["machine_id"] = options.MachineId,
            ["vendor"] = options.Vendor
        };
        var data = Encoding.UTF8.GetBytes(json.ToJsonString());
        _playerPingPacket = new ScaffoldingRequestPacket("c:player_ping", data);
    }

    public static async ValueTask<ScaffoldingClient> ConnectAsync(ScaffoldingClientOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!RoomIdHelper.IsValidRoomId(options.RoomId)) throw new ArgumentException("非法房间码。");

        var client = new ScaffoldingClient(options);

        // 启动 EasyTier
        options.EasyTierStartInfo ??= new EasyTierStartInfo();

        options.EasyTierStartInfo.NetworkName = $"scaffolding-mc-{options.RoomId[2..11]}";
        options.EasyTierStartInfo.NetworkSecret = options.RoomId[12..21];
        options.EasyTierStartInfo.MachineId = options.MachineId;
        options.EasyTierStartInfo.TcpWhiteList = ["0"];
        options.EasyTierStartInfo.UdpWhiteList = ["0"];

        await client._easyTierInstance.StartAsync(options.EasyTierStartInfo, cancellationToken);

        // 将节点信息转成玩家信息
        var host = await client.GetHostAsync();

        DebugHelper.WriteLine($"找到房主，HostName: {host.Hostname} , IP: {host.Ipv4}");


        if (!ushort.TryParse(host.Hostname[22..], out var scaffoldingPort))
        {
            client._easyTierInstance.Stop();
            throw new InvalidCastException("房主的主机名非法。");
        }

        // 连接远端 TCP
        var localPort = await client._easyTierInstance.SetPortForwardAsync(IPAddress.Parse(host.Ipv4), scaffoldingPort);
        DebugHelper.WriteLine($"本地端口: {localPort}");
        await client._tcpClient.ConnectAsync(
            IPAddress.Loopback,
            localPort
#if NET6_0_OR_GREATER
            , cancellationToken
#endif
        );
        DebugHelper.WriteLine("TcpClient 成功连接目标服务器");

        // Pipe 有关的 Task
        client._pipeCts = new CancellationTokenSource();
        _ = client.ReadPipeAsync(client._pipe.Reader, client._pipeCts.Token);
        _ = client.FillPipeAsync(client._pipe.Writer, client._pipeCts.Token);

        // Scaffolding 协议握手
        await client.HandshakeAsync(host);

        // 心跳包循环
        client._heartbeatCts = new CancellationTokenSource();
        _ = client.HeartbeatLoopAsync(client._heartbeatCts.Token);

        return client;
    }

    private async Task<EasyTierPeerInfo> GetHostAsync()
    {
        EasyTierPeerInfo? host = null;

        // 重试 10 次
        for (var i = 0; i < 10; i++)
        {
            var peerInfos = await _easyTierInstance.GetEasyTierPeerInfosAsync();
            foreach (var peerInfo in peerInfos.Where(peerInfo =>
                         peerInfo.Hostname.StartsWith("scaffolding-mc-server", StringComparison.Ordinal)))
            {
                if (host is not null) throw new InvalidCastException("同一房间中出现多个房主。");
                host = peerInfo;
            }

            if (host is not null) break;
            await Task.Delay(1000);
        }

        return host ?? throw new InvalidCastException("找不到房主。");
    }

    public async Task<ScaffoldingResponsePacket> SendRequestAsync(ScaffoldingRequestPacket packet)
    {
        ScaffoldingResponsePacket response;

        await _lock.WaitAsync();
        try
        {
            var stream = _tcpClient.GetStream();

#if NET6_0_OR_GREATER
            await stream.WriteAsync(packet.Source);
#else
            if (!MemoryMarshal.TryGetArray(packet.Source, out var segment))
                throw new InvalidOperationException("包缓冲区不由数组支持。");
            await stream.WriteAsync(segment.Array, segment.Offset, segment.Count);
#endif
            DebugHelper.WriteLine($"发送了 {packet.RequestType} 请求");
            
            response = await _responseChannel.Reader.ReadAsync();
            DebugHelper.WriteLine($"收到了响应码为 {response.ResponseStatus} 的响应");
        }
        finally
        {
            _lock.Release();
        }

        return response;
    }

    private async Task HandshakeAsync(EasyTierPeerInfo host)
    {
        DebugHelper.WriteLine("开始握手");
        // 发送第一个心跳包
        await SendRequestAsync(_playerPingPacket);

        // 协商支持的协议
        var supportedProtocolsRequest =
            new ScaffoldingRequestPacket("c:protocols", Encoding.UTF8.GetBytes(SupportedProtocols));
        var supportedProtocolsResponse = await SendRequestAsync(supportedProtocolsRequest);
        var supportedProtocolsStr = Encoding.UTF8.GetString(supportedProtocolsResponse.Data.ToArray());
        RoomInfo.SupportedRequest = supportedProtocolsStr.Split('\0');
        DebugHelper.WriteLine($"服务器支持的协议：{supportedProtocolsStr.Replace('\0', ' ')}");

        // 获取 MC 端口
        var serverPortRequest = new ScaffoldingRequestPacket("c:server_port", []);
        var mcPortResponse = await SendRequestAsync(serverPortRequest);
        var port = BinaryPrimitives.ReadUInt16BigEndian(mcPortResponse.Data.Span);
        if (mcPortResponse.ResponseStatus != 0) throw new InvalidCastException("获取服务器 MC 端口失败");
        DebugHelper.WriteLine($"获取到服务器 MC 端口：{port}");

        // 转发服务器 MC 端口转发到本地端口
        var localPort = await _easyTierInstance.SetPortForwardAsync(IPAddress.Parse(host.Ipv4), port);
        RoomInfo.Port = localPort;
        DebugHelper.WriteLine($"开始将服务器 MC 端口转发到本地端口 {localPort}");

        // 获取玩家列表
        RoomInfo.PlayerInfos = await GetPlayerListAsync();

        DebugHelper.WriteLine("握手结束");
    }

    private async Task FillPipeAsync(PipeWriter writer, CancellationToken cancellationToken = default)
    {
        var stream = _tcpClient.GetStream();

        try
        {
            while (true)
            {
                // 从 PipeWriter 获取内存块
                var memory = writer.GetMemory(512);

                // 从网络读取
#if NET6_0_OR_GREATER
                var bytesRead = await stream.ReadAsync(memory, cancellationToken);
#else
                if (!MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
                    throw new InvalidOperationException("数据包缓冲区不由数组支持。");
                var bytesRead =
                    await stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);
#endif

                if (bytesRead == 0)
                    break; // 连接关闭

                // 通知 Pipe 写入了多少字节
                writer.Advance(bytesRead);

                // 让 Reader 处理已写数据
                var result = await writer.FlushAsync(cancellationToken);
                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private async Task ReadPipeAsync(PipeReader reader, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            while (TryParseResponse(ref buffer, out var response))
            {
                await _responseChannel.Writer.WriteAsync(response, cancellationToken);
            }

            // 告诉 Pipe 哪些已消费、哪些保留
            reader.AdvanceTo(buffer.Start, buffer.End);

            // ReSharper disable once InvertIf
            if (result.IsCompleted)
            {
                if (buffer.Length > 0 && !_isDisposed)
                    throw new InvalidDataException("连接已关闭但仍有未完整数据。");

                break;
            }
        }

        await reader.CompleteAsync();
    }

    private static bool TryParseResponse(ref ReadOnlySequence<byte> buffer,
        out ScaffoldingResponsePacket response)
    {
        response = new ScaffoldingResponsePacket(null!);

        if (buffer.Length < 5)
            return false;

        Span<byte> header = stackalloc byte[5];
        buffer.Slice(0, 5).CopyTo(header);

        var dataLength = BinaryPrimitives.ReadUInt32BigEndian(header[1..]);
        var fullPacketLength = 5 + dataLength;

        if (buffer.Length < fullPacketLength)
            return false;

        var packetSource = buffer.Slice(0, fullPacketLength).ToArray();
        response = new ScaffoldingResponsePacket(packetSource);

        // 去掉已处理部分
        buffer = buffer.Slice(fullPacketLength);

        return true;
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _heartbeatStopwatch.Start();
                _ = await SendRequestAsync(_playerPingPacket);
                _heartbeatStopwatch.Stop();

                var latency = _heartbeatStopwatch.ElapsedMilliseconds;
                RoomInfo.Latency = (int)latency;
                _heartbeatStopwatch.Reset();

                RoomInfo.PlayerInfos = await GetPlayerListAsync();

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<List<ScaffoldingPlayerInfo>> GetPlayerListAsync()
    {
        var playerListRequest = new ScaffoldingRequestPacket("c:player_profiles_list", []);
        var playerListResponse = await SendRequestAsync(playerListRequest);

#if NET6_0_OR_GREATER
        var json = Encoding.UTF8.GetString(playerListResponse.Data.Span);
        return JsonSerializer.Deserialize(json, ScaffoldingPlayerInfoContext.Default.ListScaffoldingPlayerInfo)!;
#else
        var json = Encoding.UTF8.GetString(playerListResponse.Data.ToArray());
        return JsonSerializer.Deserialize<List<ScaffoldingPlayerInfo>>(json)!;
#endif
    }

    public ValueTask CloseAsync()
    {
        _tcpClient.Close();
        _heartbeatCts?.Cancel();
        _pipeCts?.Cancel();
        _easyTierInstance.Stop();

        return default;
    }

    ~ScaffoldingClient()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

#if NET6_0_OR_GREATER
[JsonSerializable(typeof(List<ScaffoldingPlayerInfo>))]
public partial class ScaffoldingPlayerInfoContext : JsonSerializerContext;
#endif