namespace BTTimeSync.Common;

public sealed class SyncResult
{
    public bool Success { get; init; }

    public double RoundTripMilliseconds { get; init; }

    public double OffsetMilliseconds { get; init; }

    public long RemoteUnixMilliseconds { get; init; }

    public DateTimeOffset RemoteTime { get; init; }

    public DateTimeOffset TargetTime { get; init; }

    public long T1 { get; init; }

    public long T2 { get; init; }

    public long T3 { get; init; }

    public long T4 { get; init; }

    public string? ErrorMessage { get; init; }
}