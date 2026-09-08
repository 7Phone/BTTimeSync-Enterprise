using BTTimeSync.Common.Models;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BTTimeSync.Bluetooth.Models;

/// <summary>
/// BTTimeSync 蓝牙连接会话。
/// </summary>
public sealed class BluetoothConnection : IDisposable
{
    /// <summary>
    /// 当前连接设备。
    /// </summary>
    public BluetoothDeviceInfo Device { get; init; } = null!;

    /// <summary>
    /// RFCOMM Socket。
    /// </summary>
    public StreamSocket Socket { get; init; } = null!;

    /// <summary>
    /// 数据读取器。
    /// </summary>
    public DataReader Reader { get; init; } = null!;

    /// <summary>
    /// 数据写入器。
    /// </summary>
    public DataWriter Writer { get; init; } = null!;

    /// <summary>
    /// 建立连接时间（UTC）。
    /// </summary>
    public DateTime ConnectedAtUtc { get; init; } = DateTime.UtcNow;

    public void Dispose()
    {
        Reader?.Dispose();
        Writer?.Dispose();
        Socket?.Dispose();
    }
}