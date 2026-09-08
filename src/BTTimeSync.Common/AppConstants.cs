namespace BTTimeSync.Common;

/// <summary>
/// BTTimeSync Enterprise 全局常量。
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// 软件名称。
    /// </summary>
    public const string ApplicationName = "BTTimeSync Enterprise";

    /// <summary>
    /// 当前产品版本。
    /// </summary>
    public const string ProductVersion = "4.1.0";

    /// <summary>
    /// 蓝牙 RFCOMM 默认服务名称。
    /// </summary>
    public const string BluetoothServiceName = "BTTimeSync";

    /// <summary>
    /// BTTimeSync RFCOMM 服务 UUID。
    /// </summary>
    public static readonly Guid BluetoothServiceUuid =
        Guid.Parse("7E4D4254-5359-4E43-9A71-54534D504C58");

    /// <summary>
    /// 默认连接超时时间，单位：毫秒。
    /// </summary>
    public const int DefaultConnectionTimeoutMs = 10000;

    /// <summary>
    /// 默认时间同步间隔，单位：秒。
    /// </summary>
    public const int DefaultSyncIntervalSeconds = 60;
}