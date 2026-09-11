using BTTimeSync.Common;

namespace BTTimeSync.Core;

/// <summary>
/// 一轮多样本时间同步结果。
/// </summary>
public sealed class TimeSyncSampleResult
{
    /// <summary>
    /// 本轮全部同步样本。
    /// </summary>
    public IReadOnlyList<SyncResult> Samples { get; init; } =
        Array.Empty<SyncResult>();

    /// <summary>
    /// 统计分析结果。
    /// </summary>
    public TimeSyncStatistics.Result Statistics { get; init; } = null!;

    /// <summary>
    /// 延迟最低的有效样本编号，从 0 开始。
    /// </summary>
    public int BestSampleIndex { get; init; }

    /// <summary>
    /// 延迟最低的有效样本。
    /// </summary>
    public SyncResult BestSample { get; init; } = null!;

    /// <summary>
    /// 最终用于校时的 Unix 时间戳，单位毫秒。
    /// </summary>
    public long TargetUnixMilliseconds { get; init; }

    /// <summary>
    /// 最终用于校时的目标 UTC 时间。
    /// </summary>
    public DateTimeOffset TargetTime { get; init; }
}