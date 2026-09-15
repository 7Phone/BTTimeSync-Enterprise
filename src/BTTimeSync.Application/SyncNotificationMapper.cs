using BTTimeSync.Common.Models;

namespace BTTimeSync.Application;

/// <summary>
/// 将校时周期结果转换为通知数据。
/// </summary>
public static class SyncNotificationMapper
{
    /// <summary>
    /// 将校时周期结果转换为通知数据。
    /// </summary>
    /// <param name="result">校时周期结果。</param>
    /// <returns>通知数据。</returns>
    public static SyncNotificationData Map(
        SyncCycleResult result)
    {
        return new SyncNotificationData
        {
            Success = result.Success,
            ConnectionLost = result.ConnectionLost,
            SyncTime = result.SyncTime,
            FinalOffsetMilliseconds =
                result.FinalOffsetMilliseconds,
            BestDelayMilliseconds =
                result.BestDelayMilliseconds,
            SampleCount =
                result.SampleCount,
            RemainingErrorMilliseconds =
                result.RemainingErrorMilliseconds,
            ErrorMessage =
                result.ErrorMessage
        };
    }
}
