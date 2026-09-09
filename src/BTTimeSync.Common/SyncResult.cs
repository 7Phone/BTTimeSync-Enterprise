namespace BTTimeSync.Common;

public class SyncResult
{
    public bool Success { get; set; }

    public double RoundTripMilliseconds { get; set; }

    public double OffsetMilliseconds { get; set; }

    public long RemoteUnixMilliseconds { get; set; }

    public DateTimeOffset RemoteTime { get; set; }

    public DateTimeOffset TargetTime { get; set; }

    public string? ErrorMessage { get; set; }
}