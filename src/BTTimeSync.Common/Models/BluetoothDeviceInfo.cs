namespace BTTimeSync.Common.Models;

/// <summary>
/// 蓝牙设备信息。
/// </summary>
public sealed class BluetoothDeviceInfo
{
    /// <summary>
    /// Windows 蓝牙设备唯一 ID。
    /// </summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// 蓝牙设备名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 蓝牙设备地址。
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// 蓝牙设备是否已配对。
    /// </summary>
    public bool IsPaired { get; init; }

    /// <summary>
    /// 蓝牙设备是否支持 BTTimeSync 服务。
    /// </summary>
    public bool IsTimeSyncDevice { get; init; }

    /// <summary>
    /// 最后发现时间（UTC）。
    /// </summary>
    public DateTime LastSeenUtc { get; init; }
}