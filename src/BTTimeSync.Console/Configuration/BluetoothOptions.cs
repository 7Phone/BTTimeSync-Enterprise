namespace BTTimeSync.Console.Configuration;

/// <summary>
/// 蓝牙相关配置。
/// </summary>
public sealed class BluetoothOptions
{
    /// <summary>
    /// 是否在启动时自动搜索并连接设备。
    /// </summary>
    public bool AutoConnect { get; set; } = true;
}