namespace BTTimeSync.Console.Configuration;

/// <summary>
/// BTTimeSync 应用程序总配置。
/// </summary>
public sealed class AppConfig
{
    /// <summary>
    /// 时间同步相关配置。
    /// </summary>
    public TimeSyncOptions TimeSync { get; set; } = new();

    /// <summary>
    /// 蓝牙相关配置。
    /// </summary>
    public BluetoothOptions Bluetooth { get; set; } = new();
}