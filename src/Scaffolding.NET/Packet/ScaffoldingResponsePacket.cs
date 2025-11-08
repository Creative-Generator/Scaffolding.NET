using System.Buffers.Binary;

namespace Scaffolding.NET.Packet;

public readonly struct ScaffoldingResponsePacket
{
    public ScaffoldingResponsePacket(ReadOnlyMemory<byte> source)
    {
        Source = source;
    }

    public ScaffoldingResponsePacket(byte responseStatus, byte[] data)
    {
        var totalLength = 1 + 4 + data.Length;

        var buffer = new byte[totalLength];
        var span = buffer.AsSpan();

        // 写入 ResponseStatus
        span[0] = responseStatus;
        
        // 写入 DataLength
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(1, 4), (uint)data.Length);

        // 写入 Data
        data.CopyTo(span[(1 + 4)..]);

        Source = buffer;
    }

    public readonly ReadOnlyMemory<byte> Source;

    public byte ResponseStatus => Source.Span[0];

    public uint DataLength => BinaryPrimitives.ReadUInt32BigEndian(Source.Span.Slice(1, 4));

    public ReadOnlyMemory<byte> Data => Source.Slice(1 + 4, (int)DataLength);
}