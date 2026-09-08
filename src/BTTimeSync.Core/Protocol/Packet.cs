namespace BTTimeSync.Core.Protocol;

/// <summary>
/// BTSP v1 数据包。
/// </summary>
public sealed class Packet
{
    /// <summary>
    /// 帧头（0x55AA）。
    /// </summary>
    public const ushort StartOfFrame = 0x55AA;

    /// <summary>
    /// 协议版本。
    /// </summary>
    public byte Version { get; init; } = 1;

    /// <summary>
    /// 数据包类型。
    /// </summary>
    public PacketType Type { get; init; }

    /// <summary>
    /// 数据负载。
    /// </summary>
    public byte[] Payload { get; init; } = [];

    /// <summary>
    /// 数据负载长度。
    /// </summary>
    public ushort Length => (ushort)Payload.Length;

    /// <summary>
    /// CRC16 校验值。
    /// </summary>
    public ushort Crc16 { get; init; }
}