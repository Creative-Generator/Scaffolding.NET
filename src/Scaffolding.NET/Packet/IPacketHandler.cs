namespace Scaffolding.NET.Packet;

public interface IPacketHandler<in TPacket>
{
    ValueTask HandleAsync(TPacket packet, Func<ValueTask> next);
}