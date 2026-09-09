using System;

namespace BTTimeSync.Core;

public class TimeSyncStatistics
{
    /// <summary>
    /// MAD 异常值检测使用的标准化系数。
    /// MAD × 1.4826 ≈ 与标准差可比较的尺度。
    /// </summary>
    private const double MadScaleFactor = 1.4826;

    /// <summary>
    /// 异常值判断阈值。
    /// 样本距离 Median 超过 3 × 1.4826 × MAD 时，
    /// 判定为异常样本。
    /// </summary>
    private const double MadThreshold = 3.0;

    /// <summary>
    /// 对 Offset 样本进行 Median / MAD 统计分析。
    /// </summary>
    /// <param name="offsets">
    /// 时间偏差样本，单位：毫秒。
    /// </param>
    /// <returns>
    /// 统计分析结果。
    /// </returns>
    public static TimeSyncStatisticsResult Analyze(
        IReadOnlyList<double> offsets)
    {
        if (offsets is null)
        {
            throw new ArgumentNullException(nameof(offsets));
        }

        if (offsets.Count == 0)
        {
            throw new ArgumentException(
                "Offset 样本不能为空。",
                nameof(offsets));
        }

        // ------------------------------------------------------------
        // 计算 Median
        // ------------------------------------------------------------

        var sorted =
            offsets
                .OrderBy(x => x)
                .ToArray();

        var median =
            CalculateMedian(sorted);

        // ------------------------------------------------------------
        // 计算每个样本与 Median 的绝对偏差
        // ------------------------------------------------------------

        var absoluteDeviations =
            offsets
                .Select(x => Math.Abs(x - median))
                .OrderBy(x => x)
                .ToArray();

        var mad =
            CalculateMedian(absoluteDeviations);

        // ------------------------------------------------------------
        // MAD 太小时：
        //
        // 如果所有样本非常接近 Median，
        // 就没有必要进行异常值剔除。
        // ------------------------------------------------------------

        if (mad <= double.Epsilon)
        {
            return new TimeSyncStatisticsResult
            {
                Median = median,
                Mad = mad,
                Threshold = 0,
                ValidIndexes =
                    Enumerable
                        .Range(0, offsets.Count)
                        .ToArray(),
                OutlierIndexes = Array.Empty<int>(),
                FinalOffset = median
            };
        }

        // ------------------------------------------------------------
        // 计算异常值判断范围
        //
        // threshold =
        //     3 × 1.4826 × MAD
        // ------------------------------------------------------------

        var threshold =
            MadThreshold *
            MadScaleFactor *
            mad;

        var validIndexes =
            new List<int>();

        var outlierIndexes =
            new List<int>();

        // ------------------------------------------------------------
        // 判断每一个样本
        // ------------------------------------------------------------

        for (var i = 0; i < offsets.Count; i++)
        {
            var distance =
                Math.Abs(offsets[i] - median);

            if (distance > threshold)
            {
                outlierIndexes.Add(i);
            }
            else
            {
                validIndexes.Add(i);
            }
        }

        // ------------------------------------------------------------
        // 安全保护：
        //
        // 如果剔除异常值后没有有效样本，
        // 则保留全部样本。
        // ------------------------------------------------------------

        if (validIndexes.Count == 0)
        {
            validIndexes =
                Enumerable
                    .Range(0, offsets.Count)
                    .ToList();

            outlierIndexes.Clear();
        }

        // ------------------------------------------------------------
        // 计算最终 Offset
        //
        // 使用剔除异常值后的 Median，
        // 而不是简单平均值。
        // ------------------------------------------------------------

        var validOffsets =
            validIndexes
                .Select(i => offsets[i])
                .OrderBy(x => x)
                .ToArray();

        var finalOffset =
            CalculateMedian(validOffsets);

        return new TimeSyncStatisticsResult
        {
            Median = median,
            Mad = mad,
            Threshold = threshold,
            ValidIndexes = validIndexes.ToArray(),
            OutlierIndexes = outlierIndexes.ToArray(),
            FinalOffset = finalOffset
        };
    }

    /// <summary>
    /// 计算一组已经排序的数据的中位数。
    /// </summary>
    private static double CalculateMedian(
        IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "数据不能为空。",
                nameof(values));
        }

        var middle =
            values.Count / 2;

        if (values.Count % 2 == 0)
        {
            return
                (values[middle - 1] +
                 values[middle]) / 2.0;
        }

        return values[middle];
    }
}

/// <summary>
/// TimeSyncStatistics 的统计分析结果。
/// </summary>
public class TimeSyncStatisticsResult
{
    /// <summary>
    /// 原始 Offset 样本的中位数，单位：毫秒。
    /// </summary>
    public double Median { get; init; }

    /// <summary>
    /// Median Absolute Deviation，
    /// 即中位绝对偏差，单位：毫秒。
    /// </summary>
    public double Mad { get; init; }

    /// <summary>
    /// 异常值判断阈值，单位：毫秒。
    /// </summary>
    public double Threshold { get; init; }

    /// <summary>
    /// 正常样本的索引。
    /// 索引与原始 Offset 样本集合对应。
    /// </summary>
    public IReadOnlyList<int> ValidIndexes { get; init; }
        = Array.Empty<int>();

    /// <summary>
    /// 异常样本的索引。
    /// 索引与原始 Offset 样本集合对应。
    /// </summary>
    public IReadOnlyList<int> OutlierIndexes { get; init; }
        = Array.Empty<int>();

    /// <summary>
    /// 剔除异常样本后的最终 Offset，
    /// 单位：毫秒。
    /// </summary>
    public double FinalOffset { get; init; }
}