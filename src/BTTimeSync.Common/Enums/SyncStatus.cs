namespace BTTimeSync.Common.Enums;

/// <summary>
/// 时间同步状态。
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// 未开始。
    /// </summary>
    NotStarted,

    /// <summary>
    /// 正在连接蓝牙设备。
    /// </summary>
    Connecting,

    /// <summary>
    /// 正在获取远程时间。
    /// </summary>
    ReadingRemoteTime,

    /// <summary>
    /// 正在计算时间偏差。
    /// </summary>
    CalculatingOffset,

    /// <summary>
    /// 正在执行时间同步。
    /// </summary>
    Synchronizing,

    /// <summary>
    /// 同步成功。
    /// </summary>
    Succeeded,

    /// <summary>
    /// 同步失败。
    /// </summary>
    Failed,

    /// <summary>
    /// 已取消。
    /// </summary>
    Cancelled
}