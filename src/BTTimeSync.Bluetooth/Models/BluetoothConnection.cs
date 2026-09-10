using BTTimeSync.Common.Models;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BTTimeSync.Bluetooth.Models;

/// <summary>
/// BTTimeSync 蓝牙连接会话。
/// </summary>
public sealed class BluetoothConnection : IDisposable
{
    public BluetoothDeviceInfo Device { get; init; } = null!;

    public StreamSocket Socket { get; init; } = null!;

    public DataReader Reader { get; init; } = null!;

    public DataWriter Writer { get; init; } = null!;

    public DateTime ConnectedAtUtc { get; init; } =
        DateTime.UtcNow;

    public void Dispose()
    {
        try
        {
            Reader.Dispose();
        }
        catch
        {
        }

        try
        {
            Writer.DetachStream();
        }
        catch
        {
        }

        try
        {
            Writer.Dispose();
        }
        catch
        {
        }

        try
        {
            Socket.Dispose();
        }
        catch
        {
        }
    }
}