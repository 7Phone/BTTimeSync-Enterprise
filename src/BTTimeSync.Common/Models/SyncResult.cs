namespace BTTimeSync.Common.Models;

/// <summary>
/// 时间同步结果。
/// </summary>
public sealed class SyncResult
{
    /// <summary>
    /// 是否同步成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 同步前本地时间。
    /// </summary>
    public DateTime LocalTimeBefore { get; init; }

    /// <summary>
    /// 同步后的本地时间。
    /// </summary>
    public DateTime LocalTimeAfter { get; init; }

    /// <summary>
    /// 远程设备时间。
    /// </summary>
    public DateTime? RemoteTime { get; init; }

    /// <summary>
    /// 计算得到的时间偏差，单位：毫秒。
    /// 正数表示远程时间领先本地时间。
    /// </summary>
    public double OffsetMilliseconds { get; init; }

    /// <summary>
    /// 操作耗时，单位：毫秒。
    /// </summary>
    public double ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 错误信息。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建一个成功的同步结果。
    /// </summary>
    public static SyncResult Succeeded(
        DateTime localTimeBefore,
        DateTime localTimeAfter,
        DateTime remoteTime,
        double offsetMilliseconds,
        double elapsedMilliseconds)
    {
        return new SyncResult
        {
            Success = true,
            LocalTimeBefore = localTimeBefore,
            LocalTimeAfter = localTimeAfter,
            RemoteTime = remoteTime,
            OffsetMilliseconds = offsetMilliseconds,
            ElapsedMilliseconds = elapsedMilliseconds
        };
    }

    /// <summary>
    /// 创建一个失败的同步结果。
    /// </summary>
    public static SyncResult Failed(
        DateTime localTimeBefore,
        double elapsedMilliseconds,
        string errorMessage)
    {
        return new SyncResult
        {
            Success = false,
            LocalTimeBefore = localTimeBefore,
            ElapsedMilliseconds = elapsedMilliseconds,
            ErrorMessage = errorMessage
        };
    }
}