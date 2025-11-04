namespace Scaffolding.NET.Packet.Handler;

internal sealed class BasicScaffoldingRequestPacketHandler : IScaffoldingRequestPacketHandler
{
    public string[] HandleableRequest { get; } =
        ["c:ping", "c:protocols", "c:server_port", "c:player_ping", "c:player_profiles_list"];
    
    public ValueTask HandleAsync(ScaffoldingServer owner, ScaffoldingRequestPacket packet, Func<ValueTask> next)
    {
        throw new NotImplementedException();
    }
}