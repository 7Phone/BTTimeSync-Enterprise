namespace BTTimeSync.Core.Protocol;

/// <summary>
/// BTTimeSync（BTSP v1）数据包类型。
/// </summary>
public enum PacketType : byte
{
    /// <summary>
    /// 握手请求。
    /// </summary>
    Hello = 0x01,

    /// <summary>
    /// 握手应答。
    /// </summary>
    HelloAck = 0x02,

    /// <summary>
    /// 请求时间。
    /// </summary>
    RequestTime = 0x10,

    /// <summary>
    /// 返回时间。
    /// </summary>
    TimeResponse = 0x11,

    /// <summary>
    /// 设置时间。
    /// </summary>
    SetTime = 0x20,

    /// <summary>
    /// Ping。
    /// </summary>
    Ping = 0x30,

    /// <summary>
    /// Pong。
    /// </summary>
    Pong = 0x31
}