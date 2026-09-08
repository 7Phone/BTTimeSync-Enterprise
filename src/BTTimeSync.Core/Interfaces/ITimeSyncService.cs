using BTTimeSync.Common.Enums;
using BTTimeSync.Common.Models;

namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// 时间同步服务接口。
/// </summary>
public interface ITimeSyncService
{
    /// <summary>
    /// 当前同步状态。
    /// </summary>
    SyncStatus Status { get; }

    /// <summary>
    /// 执行一次时间同步。
    /// </summary>
    /// <param name="device">目标蓝牙设备。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>同步结果。</returns>
    Task<SyncResult> SynchronizeAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default);
}