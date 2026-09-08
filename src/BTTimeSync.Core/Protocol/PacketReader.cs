using System.Buffers.Binary;

namespace BTTimeSync.Core.Protocol;

/// <summary>
/// BTSP 数据包解码器。
/// </summary>
public static class PacketReader
{
    /// <summary>
    /// BTSP v1 固定头长度：
    /// SOF 2 字节 + Version 1 字节 + Type 1 字节 + Length 2 字节。
    /// </summary>
    private const int HeaderLength = 6;

    /// <summary>
    /// CRC16 长度。
    /// </summary>
    private const int CrcLength = 2;

    /// <summary>
    /// 将字节数组解析为 BTSP 数据包。
    /// </summary>
    /// <param name="data">完整的数据包字节。</param>
    /// <returns>解析后的数据包。</returns>
    /// <exception cref="ArgumentException">
    /// 数据长度不足或数据包格式错误。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// CRC16 校验失败。
    /// </exception>
    public static Packet Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength + CrcLength)
        {
            throw new ArgumentException(
                "数据长度不足，无法解析 BTSP 数据包。",
                nameof(data));
        }

        var startOfFrame =
            BinaryPrimitives.ReadUInt16BigEndian(data[0..2]);

        if (startOfFrame != Packet.StartOfFrame)
        {
            throw new ArgumentException(
                $"无效的帧头：0x{startOfFrame:X4}。",
                nameof(data));
        }

        var version = data[2];

        if (version != 1)
        {
            throw new ArgumentException(
                $"不支持的 BTSP 协议版本：{version}。",
                nameof(data));
        }

        var typeValue = data[3];

        if (!Enum.IsDefined(typeof(PacketType), typeValue))
        {
            throw new ArgumentException(
                $"未知的数据包类型：0x{typeValue:X2}。",
                nameof(data));
        }

        var length =
            BinaryPrimitives.ReadUInt16BigEndian(data[4..6]);

        var expectedLength = HeaderLength + length + CrcLength;

        if (data.Length != expectedLength)
        {
            throw new ArgumentException(
                $"数据包长度错误。协议声明长度：{length}，实际数据长度：{data.Length}。",
                nameof(data));
        }

        var payload = data[6..(6 + length)].ToArray();

        var receivedCrc =
            BinaryPrimitives.ReadUInt16BigEndian(
                data[(6 + length)..(8 + length)]);

        var calculatedCrc =
            Crc16Ccitt.Compute(data[..(6 + length)]);

        if (receivedCrc != calculatedCrc)
        {
            throw new InvalidOperationException(
                $"CRC16 校验失败。收到：0x{receivedCrc:X4}，计算：0x{calculatedCrc:X4}。");
        }

        return new Packet
        {
            Version = version,
            Type = (PacketType)typeValue,
            Payload = payload,
            Crc16 = receivedCrc
        };
    }
}