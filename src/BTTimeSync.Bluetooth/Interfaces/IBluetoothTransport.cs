using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Core.Interfaces;

namespace BTTimeSync.Bluetooth.Interfaces;

public interface IBluetoothTransport : IByteTransport, IDisposable
{
    bool IsConnected { get; }

    void Attach(BluetoothConnection connection);

    Task DisconnectAsync();
}