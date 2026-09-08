using System.Buffers.Binary;

namespace BTTimeSync.Core.Protocol;

/// <summary>
/// BTSP 数据包编码器。
/// </summary>
public static class PacketWriter
{
    /// <summary>
    /// 将 Packet 编码为字节数组。
    /// </summary>
    public static byte[] Encode(Packet packet)
    {
        var buffer = new byte[8 + packet.Length];

        var span = buffer.AsSpan();

        BinaryPrimitives.WriteUInt16BigEndian(span[0..2], Packet.StartOfFrame);

        span[2] = packet.Version;
        span[3] = (byte)packet.Type;

        BinaryPrimitives.WriteUInt16BigEndian(span[4..6], packet.Length);

        packet.Payload.CopyTo(span[6..(6 + packet.Length)]);

        var crc = Crc16Ccitt.Compute(span[..(6 + packet.Length)]);

        BinaryPrimitives.WriteUInt16BigEndian(
            span[(6 + packet.Length)..(8 + packet.Length)],
            crc);

        return buffer;
    }
}