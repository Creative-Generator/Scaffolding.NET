namespace Scaffolding.NET.Packet;

public sealed class PacketPipeline<TPacket>
{
    private readonly List<Func<Func<TPacket, ValueTask>, Func<TPacket, ValueTask>>> _components = [];

    public PacketPipeline<TPacket> Use(IPacketHandler<TPacket> handler)
    {
        _components.Add(next => packet => handler.HandleAsync(packet, () => next(packet)));
        return this;
    }

    public PacketPipeline<TPacket> Use(Func<TPacket, Func<ValueTask>, ValueTask> middleware)
    {
        _components.Add(next => packet => middleware(packet, () => next(packet)));
        return this;
    }

    internal Func<TPacket, ValueTask> Build()
    {
        Func<TPacket, ValueTask> app = _ => Terminal();

        // 倒序构建
        for (var i = _components.Count - 1; i >= 0; i--)
        {
            app = _components[i](app);
        }

        return app;
        
        
        ValueTask Terminal() => new ValueTask();
    }
}
