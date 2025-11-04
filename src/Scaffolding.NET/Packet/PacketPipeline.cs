using Scaffolding.NET.Packet.Handler;

namespace Scaffolding.NET.Packet;

public sealed class PacketPipeline<TOwner, TPacket>
{
    private readonly List<Func<Func<TOwner, TPacket, ValueTask>, Func<TOwner, TPacket, ValueTask>>> _components = [];

    public PacketPipeline<TOwner, TPacket> Use(IPacketHandler<TOwner, TPacket> handler)
    {
        _components.Add(next => (owner, packet) => handler.HandleAsync(owner, packet, () => next(owner, packet)));
        return this;
    }

    public PacketPipeline<TOwner, TPacket> Use(Func<TOwner, TPacket, Func<ValueTask>, ValueTask> middleware)
    {
        _components.Add(next => (owner, packet) => middleware(owner, packet, () => next(owner, packet)));
        return this;
    }

    internal Func<TOwner, TPacket, ValueTask> Build()
    {
        Func<TOwner, TPacket, ValueTask> app = (_, _) => Terminal();

        for (var i = _components.Count - 1; i >= 0; i--)
        {
            app = _components[i](app);
        }

        return app;
    }
    
    private static ValueTask Terminal() => default;
}
