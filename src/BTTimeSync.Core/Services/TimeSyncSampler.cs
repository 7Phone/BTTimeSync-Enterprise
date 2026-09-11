using BTTimeSync.Common;
using BTTimeSync.Core.Interfaces;

namespace BTTimeSync.Core.Services;

/// <summary>
/// 时间同步多样本采集服务。
/// </summary>
public sealed class TimeSyncSampler : ITimeSyncSampler
{
    private readonly ITimeSyncService _timeSyncService;

    public TimeSyncSampler(
        ITimeSyncService timeSyncService)
    {
        _timeSyncService =
            timeSyncService ??
            throw new ArgumentNullException(
                nameof(timeSyncService));
    }

    /// <inheritdoc />
    public async Task<TimeSyncSampleResult> CollectAsync(
        int sampleCount,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        if (sampleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleCount),
                sampleCount,
                "采样次数必须大于 0。");
        }

        if (interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "采样间隔不能小于 0。");
        }

        var samples =
            new List<SyncResult>(
                sampleCount);

        for (var i = 0;
             i < sampleCount;
             i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result =
                await _timeSyncService
                    .SyncOnceAsync(
                        cancellationToken);

            samples.Add(result);

            if (i < sampleCount - 1 &&
                interval > TimeSpan.Zero)
            {
                await Task.Delay(
                    interval,
                    cancellationToken);
            }
        }

        var offsets =
            samples
                .Select(x => x.OffsetMilliseconds)
                .ToArray();

        var statistics =
            TimeSyncStatistics.Analyze(
                offsets);

        var bestSampleIndex =
            statistics.ValidIndexes
                .OrderBy(
                    index =>
                        samples[index]
                            .RoundTripMilliseconds)
                .First();

        var bestSample =
            samples[bestSampleIndex];

        var targetUnixMilliseconds =
            bestSample.T4 +
            (long)Math.Round(
                statistics.FinalOffset);

        var targetTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(
                    targetUnixMilliseconds);

        return new TimeSyncSampleResult
        {
            Samples = samples,
            Statistics = statistics,
            BestSampleIndex = bestSampleIndex,
            BestSample = bestSample,
            TargetUnixMilliseconds =
                targetUnixMilliseconds,
            TargetTime = targetTime
        };
    }
}