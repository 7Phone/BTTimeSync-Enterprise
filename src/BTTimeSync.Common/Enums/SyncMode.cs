namespace BTTimeSync.Common.Enums;

/// <summary>
/// 时间同步模式。
/// </summary>
public enum SyncMode
{
    /// <summary>
    /// 自动模式。
    /// 根据当前配置自动选择同步策略。
    /// </summary>
    Automatic,

    /// <summary>
    /// 手动模式。
    /// 由用户主动发起一次同步。
    /// </summary>
    Manual,

    /// <summary>
    /// 定时模式。
    /// 按设定的时间间隔自动执行同步。
    /// </summary>
    Scheduled
}