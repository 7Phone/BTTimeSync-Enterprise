namespace BTTimeSync.Core;

public static class TimeSyncStatistics
{
    public static Result Analyze(IReadOnlyList<double> offsets)
    {
        if (offsets.Count == 0)
            throw new ArgumentException("至少需要一个样本。");

        var sorted = offsets.OrderBy(x => x).ToArray();
        var median = Median(sorted);

        var deviations = offsets
            .Select(x => Math.Abs(x - median))
            .OrderBy(x => x)
            .ToArray();

        var mad = Median(deviations);
        var threshold = Math.Max(mad * 3.0, 1.0);

        var valid = new List<int>();
        var outlier = new List<int>();

        for (int i = 0; i < offsets.Count; i++)
        {
            if (Math.Abs(offsets[i] - median) <= threshold)
                valid.Add(i);
            else
                outlier.Add(i);
        }

        if (valid.Count == 0)
        {
            valid.Add(0);
            outlier = Enumerable.Range(1, offsets.Count - 1).ToList();
        }

        var finalOffset = Median(valid.Select(i => offsets[i]).OrderBy(x => x).ToArray());

        return new Result
        {
            Median = median,
            Mad = mad,
            Threshold = threshold,
            FinalOffset = finalOffset,
            ValidIndexes = valid,
            OutlierIndexes = outlier
        };
    }

    private static double Median(IReadOnlyList<double> values)
    {
        int m = values.Count / 2;
        return values.Count % 2 == 1
            ? values[m]
            : (values[m - 1] + values[m]) / 2.0;
    }

    public sealed class Result
    {
        public double Median { get; init; }
        public double Mad { get; init; }
        public double Threshold { get; init; }
        public double FinalOffset { get; init; }
        public IReadOnlyList<int> ValidIndexes { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> OutlierIndexes { get; init; } = Array.Empty<int>();
    }
}