namespace BTTimeSync.Common.Models;

/// <summary>
/// 蓝牙 RFCOMM 服务信息。
/// </summary>
public sealed class BluetoothRfcommServiceInfo
{
    /// <summary>
    /// RFCOMM 服务 UUID。
    /// </summary>
    public Guid ServiceUuid { get; init; }

    /// <summary>
    /// RFCOMM 服务连接名称。
    /// </summary>
    public string ConnectionServiceName { get; init; } = string.Empty;
}