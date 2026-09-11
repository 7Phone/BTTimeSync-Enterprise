using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Common.Models;

namespace BTTimeSync.Bluetooth.Services;

/// <summary>
/// Windows 蓝牙基础通信服务。
/// </summary>
/// <remarks>
/// 只负责蓝牙设备发现、RFCOMM 连接以及字节数据收发。
/// BTSP 协议、数据包以及握手逻辑由 Core 层负责。
/// </remarks>
public sealed class BluetoothService : IBluetoothService
{
    private readonly IBluetoothDiscovery _discovery;
    private readonly IBluetoothConnector _connector;
    private readonly IBluetoothTransport _transport;

    public BluetoothService()
        : this(
            new BluetoothDiscovery(),
            new BluetoothConnector(),
            new BluetoothTransport())
    {
    }

    public BluetoothService(
        IBluetoothDiscovery discovery,
        IBluetoothConnector connector,
        IBluetoothTransport transport)
    {
        _discovery =
            discovery ??
            throw new ArgumentNullException(
                nameof(discovery));

        _connector =
            connector ??
            throw new ArgumentNullException(
                nameof(connector));

        _transport =
            transport ??
            throw new ArgumentNullException(
                nameof(transport));
    }

    /// <inheritdoc />
    public bool IsConnected =>
        _transport.IsConnected;

    /// <inheritdoc />
    public Task<IReadOnlyList<BluetoothDeviceInfo>>
        DiscoverDevicesAsync(
            CancellationToken cancellationToken = default)
    {
        return _discovery
            .DiscoverDevicesAsync(
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BluetoothRfcommServiceInfo>>
        GetRfcommServicesAsync(
            BluetoothDeviceInfo device,
            CancellationToken cancellationToken = default)
    {
        return _discovery
            .GetRfcommServicesAsync(
                device,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(device);

        await DisconnectAsync();

        var connection =
            await _connector.ConnectAsync(
                device,
                cancellationToken);

        try
        {
            _transport.Attach(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public Task DisconnectAsync()
    {
        return _transport
            .DisconnectAsync();
    }

    /// <inheritdoc />
    public Task SendBytesAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        return _transport
            .SendBytesAsync(
                data,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> ReceiveBytesAsync(
        int length,
        CancellationToken cancellationToken = default)
    {
        return _transport
            .ReceiveBytesAsync(
                length,
                cancellationToken);
    }
}