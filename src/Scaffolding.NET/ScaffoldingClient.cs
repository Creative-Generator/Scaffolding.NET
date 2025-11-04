using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Scaffolding.NET.EasyTier;
using Scaffolding.NET.Helpers;
using Scaffolding.NET.Packet;
using Scaffolding.NET.Room;

namespace Scaffolding.NET;

public sealed class ScaffoldingClient : IAsyncDisposable
{
    private TcpClient _tcpClient;
    private EasyTierInstance _easyTierInstance;
    private PipeReader _pipeReader = null!;
    private PipeWriter _pipeWriter = null!;
    private Channel<ScaffoldingResponsePacket> _responseChannel;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private CancellationTokenSource? _heartbeatCts;
    private readonly Stopwatch _heartbeatStopwatch = new();
    private readonly ScaffoldingRequestPacket _playerPingPacket;

    public ScaffoldingClientOptions Options { get; init; }
    public ScaffoldingRoomInfo RoomInfo { get; private set; } = null!;

    private ScaffoldingClient(ScaffoldingClientOptions options)
    {
        Options = options;
        
        _tcpClient = new TcpClient();
        _easyTierInstance = new EasyTierInstance(options.EasyTierFileInfo);

        _responseChannel = Channel.CreateUnbounded<ScaffoldingResponsePacket>();
        
        var json = new JsonObject
        {
            ["name"] = options.PlayerName,
            ["machine_id"] = options.MachineId,
            ["vendor"] = options.Vendor
        };
        var data = Encoding.UTF8.GetBytes(json.ToJsonString());
        _playerPingPacket = new ScaffoldingRequestPacket("c:player_ping", data);
    }
    
    public static async ValueTask<ScaffoldingClient> ConnectAsync(ScaffoldingClientOptions options, CancellationToken cancellationToken = default)
    {
        if (!RoomIdHelper.IsValidRoomId(options.RoomId)) throw new ArgumentException("非法房间码。");
        
        var client = new ScaffoldingClient(options);

        // 启动 EasyTier
        options.EasyTierStartInfo ??= new EasyTierStartInfo();
        
        options.EasyTierStartInfo.NetworkName = $"scaffolding-mc-{options.MachineId[2..11]}";
        options.EasyTierStartInfo.NetworkSecret = options.MachineId[12..21];
        options.EasyTierStartInfo.MachineId = options.MachineId;
        options.EasyTierStartInfo.Dhcp = true;

        await client._easyTierInstance.StartAsync(options.EasyTierStartInfo, cancellationToken);

        // 将节点信息转成玩家信息
        var peerInfos = await client._easyTierInstance.GetEasyTierPeerInfosAsync();

        var playerInfos = new List<ScaffoldingPlayerInfo>(peerInfos.Count);
        ScaffoldingPlayerInfo? host = null;
        foreach (var playerInfo in peerInfos.Select(PlayerInfoHelper.ConvertEasyTierPeerInfoToScaffoldingPlayerInfo))
        {
            if (playerInfo.IsHost)
            {
                if (host is not null) throw new InvalidCastException("同一房间中出现多个房主。");
                host = playerInfo;
            }
            
            playerInfos.Add(playerInfo);
        }

        // 创建房间信息
        client.RoomInfo = new ScaffoldingRoomInfo
        {
            Host = host!,
            PlayerInfos = playerInfos.AsReadOnly()
        };

        if (!ushort.TryParse(host!.HostName[22..], out var scaffoldingPort))
        {
            client._easyTierInstance.Stop();
            throw new InvalidCastException("房主的主机名非法。");
        }

        // 连接远端 TCP
        var localPort = await client._easyTierInstance.SetPortForwardAsync(host.Ip, scaffoldingPort);
        await client._tcpClient.ConnectAsync(
            IPAddress.Loopback,
            localPort
#if NET6_0_OR_GREATER
            ,cancellationToken
#endif
            );

        // Pipe 配置
        var stream = client._tcpClient.GetStream();
        client._pipeReader = PipeReader.Create(stream);
        client._pipeWriter = PipeWriter.Create(stream);

        // 接收消息
        client._receiveTask = client.ReceiveLoopAsync(client._pipeReader);
        
        // Scaffolding 协议握手
        await client.HandshakeAsync();
        
        return client;
    }

    public async Task<ScaffoldingResponsePacket> SendRequestAsync(ScaffoldingRequestPacket packet)
    {
        await _pipeWriter.WriteAsync(packet.Source);

        return await _responseChannel.Reader.ReadAsync();
    }

    private async Task HandshakeAsync()
    {
        await SendRequestAsync(_playerPingPacket);

        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = HeartbeatLoopAsync(_heartbeatCts.Token);
    }
    
    private async Task ReceiveLoopAsync(PipeReader reader)
    {
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;

                var consumed = buffer.Start; // 已经处理的数据的位置
                var examined = buffer.End;   // 当前位置
                
                try
                {
                    if (TryParseResponse(ref buffer, out ScaffoldingResponsePacket response))
                    {
                        consumed = buffer.Start;
                        examined = consumed;

                        await _responseChannel.Writer.WriteAsync(response);
                    }
                    
                    if (result.IsCompleted)
                    {
                        if (buffer.Length > 0)
                        {
                            throw new InvalidDataException("房主发送了错误的数据。");
                        }

                        break;
                    }
                }
                finally
                {
                    reader.AdvanceTo(consumed, examined);
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                _heartbeatStopwatch.Start();
                await SendRequestAsync(_playerPingPacket);
                _heartbeatStopwatch.Stop();

                var latency = _heartbeatStopwatch.ElapsedMilliseconds;
                _heartbeatStopwatch.Reset();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                break;
            }
        }
    }

    private static bool TryParseResponse(ref ReadOnlySequence<byte> buffer,
        out ScaffoldingResponsePacket response)
    {
        response = new ScaffoldingResponsePacket(null!);
        if (buffer.Length < 5)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[5];
        buffer.Slice(0, 5).CopyTo(header);
        
        var dataLength = BinaryPrimitives.ReadUInt32BigEndian(header[1..]);
        
        var fullPacketLength = 5 + dataLength;
        if (buffer.Length < fullPacketLength)
        {
            return false;
        }
        
        var packetSource = buffer.Slice(0, fullPacketLength).ToArray();
        response = new ScaffoldingResponsePacket(packetSource);
            
        // 将处理过的部分去除
        buffer = buffer.Slice(fullPacketLength);
        
        return true;
    }
    
    public ValueTask CloseAsync()
    {
        _tcpClient.Close();
        return default;
    }
    
    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        
    }
}