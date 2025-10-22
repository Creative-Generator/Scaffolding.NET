namespace Scaffolding.NET.Packet;

internal sealed class StandardClientPacketHandler : IPacketHandler<ScaffoldingClientPacket>
{
    public ValueTask HandleAsync(ScaffoldingClientPacket packet, Func<ValueTask> next)
    {
        throw new NotImplementedException();
    }
}