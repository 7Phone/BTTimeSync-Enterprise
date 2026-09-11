using BTTimeSync.Core;

namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// 时间同步多样本采集服务。
/// </summary>
public interface ITimeSyncSampler
{
    /// <summary>
    /// 执行一轮多样本时间同步。
    /// </summary>
    Task<TimeSyncSampleResult> CollectAsync(
        int sampleCount,
        TimeSpan interval,
        CancellationToken cancellationToken = default);
}