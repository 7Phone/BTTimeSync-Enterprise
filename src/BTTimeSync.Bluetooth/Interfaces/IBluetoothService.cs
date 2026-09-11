using BTTimeSync.Common.Models;
using BTTimeSync.Core.Interfaces;

namespace BTTimeSync.Bluetooth.Interfaces;

/// <summary>
/// 蓝牙基础通信服务。
/// </summary>
/// <remarks>
/// Bluetooth 层只负责蓝牙设备发现、连接以及字节传输。
/// BTSP 协议由 Core 层负责。
/// </remarks>
public interface IBluetoothService : IByteTransport
{
    /// <summary>
    /// 是否已经建立蓝牙连接。
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 发现蓝牙设备。
    /// </summary>
    Task<IReadOnlyList<BluetoothDeviceInfo>>
        DiscoverDevicesAsync(
            CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定设备提供的 RFCOMM 服务。
    /// </summary>
    Task<IReadOnlyList<BluetoothRfcommServiceInfo>>
        GetRfcommServicesAsync(
            BluetoothDeviceInfo device,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立 RFCOMM 连接。
    /// </summary>
    Task ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开 RFCOMM 连接。
    /// </summary>
    Task DisconnectAsync();
}