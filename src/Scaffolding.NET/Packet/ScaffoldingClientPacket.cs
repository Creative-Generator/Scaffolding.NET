using System.Buffers.Binary;
using System.Text;

namespace Scaffolding.NET.Packet;

public struct ScaffoldingClientPacket()
{
    public ScaffoldingClientPacket(ReadOnlyMemory<byte> source) : this()
    {
        Source = source;
    }

    public ScaffoldingClientPacket(string requestType, byte[] data) : this()
    {
        if (RequestType.Length > 255)
        {
            throw new ArgumentException("请求类型长度不能大于 255。", nameof(requestType));
        }

        var typeBytes = Encoding.UTF8.GetBytes(requestType);
        var totalLength = 1 + typeBytes.Length + 4 + data.Length;

        var buffer = new byte[totalLength];
        var span = buffer.AsSpan();

        // 写入 RequestTypeLength
        span[0] = (byte)typeBytes.Length;

        // 写入 RequestType
        typeBytes.CopyTo(span.Slice(1, typeBytes.Length));

        // 写入 DataLength
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(1 + typeBytes.Length, 4), (uint)data.Length);

        // 写入 Data
        data.CopyTo(span.Slice(1 + typeBytes.Length + 4));

        Source = buffer;
    }

    public readonly ReadOnlyMemory<byte> Source;

    public byte RequestTypeLength => Source.Span[0];

    public string RequestType
    {
        get
        {
            var data = Source.Slice(1, RequestTypeLength);
            return Encoding.UTF8.GetString(data.ToArray());
        }
    }

    public uint DataLength => BinaryPrimitives.ReadUInt32BigEndian(Source.Span.Slice(1 + RequestTypeLength, 4));

    public ReadOnlyMemory<byte> Data => Source.Slice(1 + RequestTypeLength + 4, (int)DataLength);
}