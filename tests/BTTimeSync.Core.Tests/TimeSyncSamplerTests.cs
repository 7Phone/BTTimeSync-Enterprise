using BTTimeSync.Common;
using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Services;

namespace BTTimeSync.Core.Tests;

public sealed class TimeSyncSamplerTests
{
    [Fact]
    public async Task CollectAsync_WithMultipleSamples_ReturnsAllSamples()
    {
        var results =
            new[]
            {
                CreateSyncResult(
                    delay: 30,
                    offset: 10,
                    t4: 1_000_000),

                CreateSyncResult(
                    delay: 20,
                    offset: 11,
                    t4: 1_000_100),

                CreateSyncResult(
                    delay: 40,
                    offset: 9,
                    t4: 1_000_200)
            };

        var service =
            new FakeTimeSyncService(
                results);

        var sampler =
            new TimeSyncSampler(
                service);

        var result =
            await sampler.CollectAsync(
                sampleCount: 3,
                interval: TimeSpan.Zero);

        Assert.Equal(
            3,
            result.Samples.Count);

        Assert.Equal(
            3,
            service.CallCount);
    }

    [Fact]
    public async Task CollectAsync_SelectsLowestDelaySample()
    {
        var results =
            new[]
            {
                CreateSyncResult(
                    delay: 50,
                    offset: 10,
                    t4: 1_000_000),

                CreateSyncResult(
                    delay: 15,
                    offset: 11,
                    t4: 1_000_100),

                CreateSyncResult(
                    delay: 30,
                    offset: 9,
                    t4: 1_000_200)
            };

        var sampler =
            new TimeSyncSampler(
                new FakeTimeSyncService(
                    results));

        var result =
            await sampler.CollectAsync(
                sampleCount: 3,
                interval: TimeSpan.Zero);

        Assert.Equal(
            1,
            result.BestSampleIndex);

        Assert.Equal(
            15,
            result.BestSample.RoundTripMilliseconds);
    }

    [Fact]
    public async Task CollectAsync_ComputesFinalOffset()
    {
        var results =
            new[]
            {
                CreateSyncResult(
                    delay: 20,
                    offset: 10,
                    t4: 1_000_000),

                CreateSyncResult(
                    delay: 25,
                    offset: 12,
                    t4: 1_000_100),

                CreateSyncResult(
                    delay: 30,
                    offset: 11,
                    t4: 1_000_200)
            };

        var sampler =
            new TimeSyncSampler(
                new FakeTimeSyncService(
                    results));

        var result =
            await sampler.CollectAsync(
                sampleCount: 3,
                interval: TimeSpan.Zero);

        Assert.Equal(
            11,
            result.Statistics.FinalOffset);
    }

    [Fact]
    public async Task CollectAsync_ComputesTargetFromBestSampleT4AndFinalOffset()
    {
        var results =
            new[]
            {
                CreateSyncResult(
                    delay: 50,
                    offset: 10,
                    t4: 1_000_000),

                CreateSyncResult(
                    delay: 10,
                    offset: 12,
                    t4: 2_000_000),

                CreateSyncResult(
                    delay: 30,
                    offset: 11,
                    t4: 3_000_000)
            };

        var sampler =
            new TimeSyncSampler(
                new FakeTimeSyncService(
                    results));

        var result =
            await sampler.CollectAsync(
                sampleCount: 3,
                interval: TimeSpan.Zero);

        Assert.Equal(
            2_000_011,
            result.TargetUnixMilliseconds);
    }

    [Fact]
    public async Task CollectAsync_WithZeroSampleCount_Throws()
    {
        var sampler =
            new TimeSyncSampler(
                new FakeTimeSyncService(
                    Array.Empty<SyncResult>()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () =>
                sampler.CollectAsync(
                    sampleCount: 0,
                    interval: TimeSpan.Zero));
    }

    [Fact]
    public async Task CollectAsync_WithNegativeInterval_Throws()
    {
        var sampler =
            new TimeSyncSampler(
                new FakeTimeSyncService(
                    Array.Empty<SyncResult>()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () =>
                sampler.CollectAsync(
                    sampleCount: 1,
                    interval: TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public async Task CollectAsync_HonorsSampleInterval()
    {
        var results =
            new[]
            {
                CreateSyncResult(
                    delay: 10,
                    offset: 10,
                    t4: 1_000_000),

                CreateSyncResult(
                    delay: 10,
                    offset: 10,
                    t4: 1_000_100)
            };

        var sampler =
            new TimeSyncSampler(
                new FakeTimeSyncService(
                    results));

        var stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        await sampler.CollectAsync(
            sampleCount: 2,
            interval: TimeSpan.FromMilliseconds(20));

        stopwatch.Stop();

        Assert.True(
            stopwatch.ElapsedMilliseconds >= 15);
    }

    private static SyncResult CreateSyncResult(
        double delay,
        double offset,
        long t4)
    {
        return new SyncResult
        {
            Success = true,
            RoundTripMilliseconds = delay,
            OffsetMilliseconds = offset,
            RemoteUnixMilliseconds = t4,
            RemoteTime =
                DateTimeOffset
                    .FromUnixTimeMilliseconds(
                        t4),
            TargetTime =
                DateTimeOffset
                    .FromUnixTimeMilliseconds(
                        t4)
                    .AddMilliseconds(
                        offset),
            T1 = t4 - (long)delay,
            T2 = t4 - (long)delay / 2,
            T3 = t4 - (long)delay / 2,
            T4 = t4
        };
    }

    private sealed class FakeTimeSyncService
        : ITimeSyncService
    {
        private readonly Queue<SyncResult> _results;

        public int CallCount { get; private set; }

        public FakeTimeSyncService(
            IEnumerable<SyncResult> results)
        {
            _results =
                new Queue<SyncResult>(
                    results);
        }

        public Task<SyncResult> SyncOnceAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "FakeTimeSyncService 没有更多测试样本。");
            }

            return Task.FromResult(
                _results.Dequeue());
        }
    }
}