namespace Scaffolding.NET.Packet.Handler;

public interface IPacketHandler<in TOwner, in TPacket>
{
    ValueTask HandleAsync(TOwner owner, TPacket packet, Func<ValueTask> next);
}