namespace BTTimeSync.Common.Models;

/// <summary>
/// 单次完整校时周期的结果。
/// 用于校时主程序与其他组件之间传递校时结果。
/// </summary>
public sealed class SyncCycleResult
{
    /// <summary>
    /// 校时是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 蓝牙连接是否在校时过程中断开。
    /// </summary>
    public bool ConnectionLost { get; init; }

    /// <summary>
    /// 校时时间。
    /// </summary>
    public DateTimeOffset SyncTime { get; init; }

    /// <summary>
    /// 最终计算得到的时间偏移量，单位：毫秒。
    /// </summary>
    public double FinalOffsetMilliseconds { get; init; }

    /// <summary>
    /// 最佳样本的往返延迟，单位：毫秒。
    /// </summary>
    public double BestDelayMilliseconds { get; init; }

    /// <summary>
    /// 本次实际采集的样本数量。
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// 校时后验证得到的剩余误差，单位：毫秒。
    /// </summary>
    public double RemainingErrorMilliseconds { get; init; }

    /// <summary>
    /// 校时失败或异常时的错误信息。
    /// </summary>
    public string? ErrorMessage { get; init; }
}
