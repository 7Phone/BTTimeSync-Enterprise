using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Common.Models;

namespace BTTimeSync.Bluetooth.Interfaces;

/// <summary>
/// RFCOMM 蓝牙连接服务。
/// </summary>
public interface IBluetoothConnector
{
    Task<BluetoothConnection> ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default);
}