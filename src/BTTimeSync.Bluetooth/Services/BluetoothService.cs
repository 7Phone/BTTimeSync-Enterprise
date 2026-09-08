using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Common;
using BTTimeSync.Common.Models;
using BTTimeSync.Core.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BTTimeSync.Bluetooth.Services;

/// <summary>
/// Windows 蓝牙通信服务。
/// </summary>
public sealed class BluetoothService : IBluetoothService
{
    private BluetoothConnection? _connection;

    /// <summary>
    /// 当前是否已连接。
    /// </summary>
    public bool IsConnected => _connection is not null;

    /// <summary>
    /// 扫描蓝牙设备。
    /// </summary>
    public async Task<IReadOnlyList<BluetoothDeviceInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selector = BluetoothDevice.GetDeviceSelector();
        var devices = await DeviceInformation.FindAllAsync(selector);

        var result = new List<BluetoothDeviceInfo>();

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bluetoothDevice =
                await BluetoothDevice.FromIdAsync(device.Id);

            if (bluetoothDevice is null)
            {
                continue;
            }

            var rfcommResult =
                await bluetoothDevice.GetRfcommServicesAsync();

            var isTimeSyncDevice = rfcommResult.Services.Any(
                service =>
                    service.ServiceId.Uuid ==
                    AppConstants.BluetoothServiceUuid);

            result.Add(new BluetoothDeviceInfo
            {
                DeviceId = device.Id,
                Name = bluetoothDevice.Name,
                Address = FormatBluetoothAddress(
                    bluetoothDevice.BluetoothAddress),
                IsPaired = device.Pairing.IsPaired,
                IsTimeSyncDevice = isTimeSyncDevice,
                LastSeenUtc = DateTime.UtcNow
            });
        }

        return result;
    }

    /// <summary>
    /// 获取指定蓝牙设备提供的 RFCOMM 服务。
    /// </summary>
    public async Task<IReadOnlyList<BluetoothRfcommServiceInfo>>
        GetRfcommServicesAsync(
            BluetoothDeviceInfo deviceInfo,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bluetoothDevice =
            await BluetoothDevice.FromIdAsync(deviceInfo.DeviceId);

        if (bluetoothDevice is null)
        {
            return Array.Empty<BluetoothRfcommServiceInfo>();
        }

        var result = await bluetoothDevice.GetRfcommServicesAsync();

        var services = new List<BluetoothRfcommServiceInfo>();

        foreach (var service in result.Services)
        {
            services.Add(new BluetoothRfcommServiceInfo
            {
                ServiceUuid = service.ServiceId.Uuid,
                ConnectionServiceName =
                    service.ConnectionServiceName
            });
        }

        return services;
    }

    /// <summary>
    /// 连接指定的 BTTimeSync 蓝牙设备。
    /// </summary>
    public async Task ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await DisconnectAsync();

        using var bluetoothDevice =
            await BluetoothDevice.FromIdAsync(device.DeviceId);

        if (bluetoothDevice is null)
        {
            throw new InvalidOperationException(
                "无法打开蓝牙设备。");
        }

        var serviceResult =
            await bluetoothDevice.GetRfcommServicesForIdAsync(
                RfcommServiceId.FromUuid(
                    AppConstants.BluetoothServiceUuid));

        var service = serviceResult.Services.FirstOrDefault();

        if (service is null)
        {
            throw new InvalidOperationException(
                "目标设备未提供 BTTimeSync RFCOMM 服务。");
        }

        var socket = new StreamSocket();

        await socket.ConnectAsync(
            service.ConnectionHostName,
            service.ConnectionServiceName);

        _connection = new BluetoothConnection
        {
            Device = device,
            Socket = socket,
            Reader = new DataReader(socket.InputStream),
            Writer = new DataWriter(socket.OutputStream),
            ConnectedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 断开当前蓝牙连接。
    /// </summary>
    public Task DisconnectAsync()
    {
        _connection?.Dispose();
        _connection = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 向当前连接的蓝牙设备发送 BTSP 数据包。
    /// </summary>
    public async Task SendPacketAsync(
        Packet packet,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connection is null)
        {
            throw new InvalidOperationException(
                "当前没有已建立的蓝牙连接。");
        }

        var data = PacketWriter.Encode(packet);

        _connection.Writer.WriteBytes(data);

        await _connection.Writer.StoreAsync();
    }

    /// <summary>
    /// 从当前连接的蓝牙设备接收一个 BTSP 数据包。
    /// </summary>
    public async Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connection is null)
        {
            throw new InvalidOperationException(
                "当前没有已建立的蓝牙连接。");
        }

        var reader = _connection.Reader;

        // BTSP 固定头：
        // SOF 2 字节
        // Version 1 字节
        // Type 1 字节
        // Length 2 字节
        const uint headerLength = 6;

        // 先读取固定头。
        await LoadExactlyAsync(
            reader,
            headerLength,
            cancellationToken);

        var header = new byte[headerLength];
        reader.ReadBytes(header);

        // 从头部读取 Payload 长度。
        var payloadLength =
            (header[4] << 8) |
            header[5];

        // Payload + CRC16。
        var remainingLength =
            (uint)payloadLength + 2;

        await LoadExactlyAsync(
            reader,
            remainingLength,
            cancellationToken);

        var remaining = new byte[remainingLength];
        reader.ReadBytes(remaining);

        // 重新组合成完整 BTSP 数据包。
        var packetData =
            new byte[header.Length + remaining.Length];

        System.Buffer.BlockCopy(
            header,
            0,
            packetData,
            0,
            header.Length);

        System.Buffer.BlockCopy(
            remaining,
            0,
            packetData,
            header.Length,
            remaining.Length);

        // 使用 Core 层统一完成协议解析和 CRC 校验。
        return PacketReader.Decode(packetData);
    }

    /// <summary>
    /// 确保 DataReader 至少加载指定数量的数据。
    /// </summary>
    private static async Task LoadExactlyAsync(
        DataReader reader,
        uint requiredLength,
        CancellationToken cancellationToken)
    {
        while (reader.UnconsumedBufferLength < requiredLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var missingLength =
                requiredLength - reader.UnconsumedBufferLength;

            var loadedLength =
                await reader.LoadAsync(missingLength);

            if (loadedLength == 0)
            {
                throw new InvalidOperationException(
                    "蓝牙连接已关闭，未能读取完整的 BTSP 数据包。");
            }
        }
    }

    /// <summary>
    /// 将蓝牙地址格式化为 XX:XX:XX:XX:XX:XX。
    /// </summary>
    private static string FormatBluetoothAddress(ulong address)
    {
        return address.ToString("X12")
            .Insert(2, ":")
            .Insert(5, ":")
            .Insert(8, ":")
            .Insert(11, ":")
            .Insert(14, ":");
    }
}