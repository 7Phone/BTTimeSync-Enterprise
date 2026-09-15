namespace BTTimeSync.Common.Models;

/// <summary>
/// BTTimeSync 校时通知数据。
/// 用于校时主程序与通知程序之间传递一次校时结果。
/// </summary>
public sealed class SyncNotificationData
{
    /// <summary>
    /// 校时是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 是否发生蓝牙连接断开。
    /// </summary>
    public bool ConnectionLost { get; init; }

    /// <summary>
    /// 校时完成时间。
    /// </summary>
    public DateTimeOffset SyncTime { get; init; }

    /// <summary>
    /// 最终校时偏移量，单位：毫秒。
    /// </summary>
    public double FinalOffsetMilliseconds { get; init; }

    /// <summary>
    /// 最佳样本的往返延迟，单位：毫秒。
    /// </summary>
    public double BestDelayMilliseconds { get; init; }

    /// <summary>
    /// 本次校时使用的样本数量。
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// 校时后的剩余误差，单位：毫秒。
    /// </summary>
    public double RemainingErrorMilliseconds { get; init; }

    /// <summary>
    /// 失败或异常原因。
    /// </summary>
    public string? ErrorMessage { get; init; }
}
