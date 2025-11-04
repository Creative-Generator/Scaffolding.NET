namespace Scaffolding.NET.Packet.Handler;

public interface IScaffoldingRequestPacketHandler : IPacketHandler<ScaffoldingServer, ScaffoldingRequestPacket>
{
    public string[] HandleableRequest { get; }
}