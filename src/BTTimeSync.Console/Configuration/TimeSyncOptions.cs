namespace BTTimeSync.Console.Configuration;

/// <summary>
/// 时间同步相关配置。
/// </summary>
public sealed class TimeSyncOptions
{
    /// <summary>
    /// 每次时间同步采集的样本数量。
    /// </summary>
    public int SampleCount { get; set; } = 10;

    /// <summary>
    /// 相邻样本之间的采样间隔，单位：毫秒。
    /// </summary>
    public int SampleIntervalMilliseconds { get; set; } = 100;

    /// <summary>
    /// 自动同步间隔，单位：分钟。
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// 时间校准后的验证允许误差，单位：毫秒。
    /// </summary>
    public double VerificationThresholdMilliseconds { get; set; } = 50;

    /// <summary>
    /// 蓝牙重连初始等待时间，单位：秒。
    /// </summary>
    public int ReconnectRetryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 蓝牙重连等待时间的最大值，单位：秒。
    /// </summary>
    public int ReconnectRetryIntervalMaximumSeconds { get; set; } = 30;
}