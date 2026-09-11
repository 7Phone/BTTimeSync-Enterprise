using BTTimeSync.Common;

namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// 单次时间同步服务。
/// </summary>
public interface ITimeSyncService
{
    /// <summary>
    /// 执行一次时间同步。
    /// </summary>
    Task<SyncResult> SyncOnceAsync(
        CancellationToken cancellationToken = default);
}