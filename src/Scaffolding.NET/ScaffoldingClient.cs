using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
// ReSharper disable once RedundantUsingDirective
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
// ReSharper disable once RedundantUsingDirective
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
    private ScaffoldingRequestPacket _playerPingPacket;
    private readonly TcpClient _tcpClient;
    private readonly EasyTierInstance _easyTierInstance;
    private readonly Pipe _pipe;
    private readonly Channel<ScaffoldingResponsePacket> _responseChannel;
    private readonly Stopwatch _heartbeatStopwatch = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ScaffoldingClientOptions _options;

    private string[] _supportedProtocols =
        ["c:ping", "c:protocols", "c:server_port", "c:player_ping", "c:player_profiles_list", "c:player_easytier_id"];

    public ScaffoldingRoomInfo RoomInfo { get; }

    public event EventHandler? Disposed;

    private ScaffoldingClient(ScaffoldingClientOptions options)
    {
        _options = options;
        _tcpClient = new TcpClient();
        _easyTierInstance = new EasyTierInstance(options.EasyTierFileInfo);
        _responseChannel = Channel.CreateUnbounded<ScaffoldingResponsePacket>();
        _pipe = new Pipe();
        RoomInfo = new ScaffoldingRoomInfo();
    }

    public static async ValueTask<ScaffoldingClient> ConnectAsync(ScaffoldingClientOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!RoomIdHelper.IsValidRoomId(options.RoomId)) throw new ArgumentException("非法房间码。");

        var client = new ScaffoldingClient(options);

        client.RoomInfo.RoomId = options.RoomId;
        client._supportedProtocols = client._supportedProtocols.Union(options.AdvancedSupportedProtocols).ToArray();
        client._playerPingPacket = await client.GetPlayerPingPacketAsync(options, false);

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
        DebugHelper.WriteLine($"房主映射的本地端口: {localPort}");
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

    public Task<ScaffoldingResponsePacket> SendRequestAsync(ScaffoldingRequestPacket packet,
        CancellationToken cancellationToken = default) => SendRequestAsync(packet, false, cancellationToken);

    public async Task<ScaffoldingResponsePacket> SendRequestAsync(ScaffoldingRequestPacket packet, bool bypassCheck,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed) throw new ObjectDisposedException("对象已被释放。");
        if (!bypassCheck)
        {
            if (!_supportedProtocols.Contains(packet.RequestType)) throw new ArgumentException("发送了服务器不支持的请求。");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 秒超时

        await _lock.WaitAsync(cts.Token);
        try
        {
            var stream = _tcpClient.GetStream();

#if NET6_0_OR_GREATER
            await stream.WriteAsync(packet.Source, cts.Token);
#else
            if (!MemoryMarshal.TryGetArray(packet.Source, out var segment))
                throw new InvalidOperationException("包缓冲区不由数组支持。");
            await stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cts.Token);
#endif

            DebugHelper.WriteLine($"发送了 {packet.RequestType} 请求");

            // 从通道读取响应
            var response = await _responseChannel.Reader.ReadAsync(cts.Token);
            DebugHelper.WriteLine($"收到了响应码为 {response.ResponseStatus} 的响应");

            return response;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task HandshakeAsync(EasyTierPeerInfo host)
    {
        DebugHelper.WriteLine("开始握手");
        // 发送第一个心跳包
        await SendRequestAsync(_playerPingPacket);

        // 协商支持的协议
        var supportedProtocolsRequest =
            new ScaffoldingRequestPacket("c:protocols", Encoding.UTF8.GetBytes(string.Join("\0", _supportedProtocols)));
        var supportedProtocolsResponse = await SendRequestAsync(supportedProtocolsRequest);
        var supportedProtocolsStr = Encoding.UTF8.GetString(supportedProtocolsResponse.Data.ToArray());
        RoomInfo.SupportedProtocols = supportedProtocolsStr.Split('\0');
        DebugHelper.WriteLine($"服务器支持的协议：{supportedProtocolsStr.Replace('\0', ' ')}");

        // c:player_easytier_id 额外处理
        if (RoomInfo.SupportedProtocols.Contains("c:player_easytier_id"))
        {
            _playerPingPacket = await GetPlayerPingPacketAsync(_options, true);
        }

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
            while (!cancellationToken.IsCancellationRequested)
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
        while (!cancellationToken.IsCancellationRequested)
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

    private async Task<ScaffoldingRequestPacket> GetPlayerPingPacketAsync(ScaffoldingClientOptions options,
        bool withEasyTierNodeId)
    {
        var json = new JsonObject
        {
            ["name"] = options.PlayerName,
            ["machine_id"] = options.MachineId,
            ["vendor"] = options.Vendor ??
                         $"Scaffolding.NET, EasyTier v{await options.EasyTierFileInfo.GetEasyTierVersionAsync()}"
        };

        if (withEasyTierNodeId)
        {
            var output = await _easyTierInstance.CallEasyTierCliAsync("node");
            using var doc = JsonDocument.Parse(output!);
            var peerId = doc.RootElement.GetProperty("peer_id").GetInt64();
            json["easytier_id"] = peerId.ToString();
        }

        var data = Encoding.UTF8.GetBytes(json.ToJsonString());
        return new ScaffoldingRequestPacket("c:player_ping", data);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken = default)
    {
        var timeoutCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _heartbeatStopwatch.Start();
                _ = await SendRequestAsync(_playerPingPacket, cancellationToken);
                _heartbeatStopwatch.Stop();

                var latency = _heartbeatStopwatch.ElapsedMilliseconds;
                RoomInfo.Latency = (int)latency;
                _heartbeatStopwatch.Reset();

                RoomInfo.PlayerInfos = await GetPlayerListAsync();

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 可能是外部取消或超时
                if (!cancellationToken.IsCancellationRequested)
                {
                    timeoutCount++;
                    DebugHelper.WriteLine($"心跳超时 ({timeoutCount}/3)");
                    if (timeoutCount < 3) continue;
                    DebugHelper.WriteLine("连续 3 次心跳超时，断开连接");
                    await DisposeAsync();
                }

                // 外部主动取消
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

    ~ScaffoldingClient()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

#pragma warning disable CS1998
    public async ValueTask DisposeAsync()
#pragma warning restore CS1998
    {
        if (_isDisposed) return;
        
        // 释放 TcpClient 和相关资源
        _tcpClient.GetStream().Dispose();
        _tcpClient.Dispose();
        
        // 释放 CancellationTokenSource
#if NET8_0_OR_GREATER
        if (_heartbeatCts is not null) await _heartbeatCts.CancelAsync();
        if (_pipeCts is not null) await _pipeCts.CancelAsync();
#else
        _heartbeatCts?.Cancel();
        _pipeCts?.Cancel();
#endif
        _heartbeatCts?.Dispose();
        _pipeCts?.Dispose();
        
        // 释放 EasyTierInstance
        _easyTierInstance.Stop();
        _easyTierInstance.Dispose();
        
        // 完成 Pipe
        await _pipe.Reader.CompleteAsync();
        await _pipe.Writer.CompleteAsync();
        
        _isDisposed = true;

        Disposed?.Invoke(this, EventArgs.Empty);

        GC.SuppressFinalize(this);
    }
}

#if NET6_0_OR_GREATER
[JsonSerializable(typeof(List<ScaffoldingPlayerInfo>))]
public partial class ScaffoldingPlayerInfoContext : JsonSerializerContext;
#endif