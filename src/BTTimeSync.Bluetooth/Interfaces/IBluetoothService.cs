using BTTimeSync.Common.Models;
using BTTimeSync.Core.Protocol;

namespace BTTimeSync.Bluetooth.Interfaces;

/// <summary>
/// 蓝牙通信服务。
/// </summary>
public interface IBluetoothService
{
    /// <summary>
    /// 扫描蓝牙设备。
    /// </summary>
    Task<IReadOnlyList<BluetoothDeviceInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定蓝牙设备提供的 RFCOMM 服务。
    /// </summary>
    Task<IReadOnlyList<BluetoothRfcommServiceInfo>> GetRfcommServicesAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 连接指定的 BTTimeSync 蓝牙设备。
    /// </summary>
    Task ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 断开当前蓝牙连接。
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 向当前连接的蓝牙设备发送 BTSP 数据包。
    /// </summary>
    Task SendPacketAsync(
        Packet packet,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从当前连接的蓝牙设备接收一个 BTSP 数据包。
    /// </summary>
    Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 当前是否已连接。
    /// </summary>
    bool IsConnected { get; }
}