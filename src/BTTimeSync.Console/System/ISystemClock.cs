namespace BTTimeSync.Console.Infrastructure;

/// <summary>
/// 系统时钟操作接口。
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// 设置系统 UTC 时间。
    /// </summary>
    void SetUtcTime(DateTimeOffset utcTime);
}