using BTTimeSync.Common.Models;

namespace BTTimeSync.Bluetooth.Interfaces;

/// <summary>
/// 蓝牙设备发现服务。
/// </summary>
public interface IBluetoothDiscovery
{
    Task<IReadOnlyList<BluetoothDeviceInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BluetoothRfcommServiceInfo>>
        GetRfcommServicesAsync(
            BluetoothDeviceInfo device,
            CancellationToken cancellationToken = default);
}