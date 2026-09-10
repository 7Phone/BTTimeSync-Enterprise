using BTTimeSync.Common;

namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// 时间同步服务接口。
/// </summary>
public interface ITimeSyncService
{
    Task<SyncResult> SyncOnceAsync(
        CancellationToken cancellationToken = default);
}