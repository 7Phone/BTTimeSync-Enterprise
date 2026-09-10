using BTTimeSync.Common.Models;
using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Protocol;

namespace BTTimeSync.Bluetooth.Interfaces;

/// <summary>
/// 蓝牙通信服务。
/// </summary>
public interface IBluetoothService : IByteTransport
{
    Task<IReadOnlyList<BluetoothDeviceInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BluetoothRfcommServiceInfo>>
        GetRfcommServicesAsync(
            BluetoothDeviceInfo device,
            CancellationToken cancellationToken = default);

    Task ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task SendPacketAsync(
        Packet packet,
        CancellationToken cancellationToken = default);

    Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken = default);

    bool IsConnected { get; }
}