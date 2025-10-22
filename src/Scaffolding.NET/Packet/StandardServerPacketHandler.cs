namespace Scaffolding.NET.Packet;

public sealed class StandardServerPacketHandler : IPacketHandler<ScaffoldingServerPacket>
{
    public ValueTask HandleAsync(ScaffoldingServerPacket packet, Func<ValueTask> next)
    {
        throw new NotImplementedException();
    }
}