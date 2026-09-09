namespace BTTimeSync.Common;

public class TimeSyncTimestamps
{
    /// <summary>
    /// T1：客户端发送 TimeRequest 的时间戳。
    /// </summary>
    public long T1 { get; set; }

    /// <summary>
    /// T2：服务器收到 TimeRequest 的时间戳。
    /// </summary>
    public long T2 { get; set; }

    /// <summary>
    /// T3：服务器发送 TimeResponse 的时间戳。
    /// </summary>
    public long T3 { get; set; }

    /// <summary>
    /// T4：客户端收到 TimeResponse 的时间戳。
    /// </summary>
    public long T4 { get; set; }
}