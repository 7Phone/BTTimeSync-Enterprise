using BTTimeSync.Core;

namespace BTTimeSync.Core.Tests;

public sealed class TimeSyncStatisticsTests
{
    [Fact]
    public void Analyze_WithOddNumberOfSamples_ReturnsCorrectMedian()
    {
        var offsets =
            new[]
            {
                10.0,
                12.0,
                11.0,
                13.0,
                9.0
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Equal(
            11.0,
            result.Median);

        Assert.Equal(
            11.0,
            result.FinalOffset);
    }

    [Fact]
    public void Analyze_WithEvenNumberOfSamples_ReturnsAverageMedian()
    {
        var offsets =
            new[]
            {
                10.0,
                12.0,
                14.0,
                16.0
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Equal(
            13.0,
            result.Median);

        Assert.Equal(
            13.0,
            result.FinalOffset);
    }

    [Fact]
    public void Analyze_WithStableSamples_MarksAllSamplesValid()
    {
        var offsets =
            new[]
            {
                10.0,
                10.5,
                9.5,
                10.2,
                9.8
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Equal(
            5,
            result.ValidIndexes.Count);

        Assert.Empty(
            result.OutlierIndexes);
    }

    [Fact]
    public void Analyze_WithLargeOutlier_RejectsOutlier()
    {
        var offsets =
            new[]
            {
                10.0,
                10.5,
                9.5,
                10.2,
                1000.0
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Contains(
            4,
            result.OutlierIndexes);

        Assert.DoesNotContain(
            4,
            result.ValidIndexes);
    }

    [Fact]
    public void Analyze_FinalOffset_UsesValidSamplesOnly()
    {
        var offsets =
            new[]
            {
                100.0,
                101.0,
                99.0,
                100.5,
                10000.0
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Equal(
            100.25,
            result.FinalOffset);
    }

    [Fact]
    public void Analyze_WithSingleSample_ReturnsThatSample()
    {
        var offsets =
            new[]
            {
                25.0
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Equal(
            25.0,
            result.Median);

        Assert.Equal(
            25.0,
            result.FinalOffset);

        Assert.Single(
            result.ValidIndexes);
    }

    [Fact]
    public void Analyze_WithEmptySamples_Throws()
    {
        var offsets =
            Array.Empty<double>();

        Assert.Throws<ArgumentException>(
            () =>
                TimeSyncStatistics.Analyze(
                    offsets));
    }

    [Fact]
    public void Analyze_WithIdenticalSamples_HasZeroMad()
    {
        var offsets =
            new[]
            {
                10.0,
                10.0,
                10.0,
                10.0,
                10.0
            };

        var result =
            TimeSyncStatistics.Analyze(
                offsets);

        Assert.Equal(
            0.0,
            result.Mad);

        Assert.Equal(
            1.0,
            result.Threshold);

        Assert.Equal(
            10.0,
            result.FinalOffset);
    }
}