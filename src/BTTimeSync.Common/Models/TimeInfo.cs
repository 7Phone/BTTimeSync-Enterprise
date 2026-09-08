namespace BTTimeSync.Common.Models;

/// <summary>
/// 时间信息。
/// 用于表示本机或远程设备的时间数据。
/// </summary>
public sealed class TimeInfo
{
    /// <summary>
    /// UTC 时间。
    /// </summary>
    public DateTime UtcTime { get; init; }

    /// <summary>
    /// Unix 时间戳（毫秒）。
    /// </summary>
    public long UnixMilliseconds { get; init; }

    /// <summary>
    /// 创建当前时间信息。
    /// </summary>
    public static TimeInfo Now()
    {
        var utcNow = DateTime.UtcNow;

        return new TimeInfo
        {
            UtcTime = utcNow,
            UnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}