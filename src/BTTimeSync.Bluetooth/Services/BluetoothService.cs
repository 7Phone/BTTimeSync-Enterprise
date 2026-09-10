using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Common;
using BTTimeSync.Common.Models;
using BTTimeSync.Core.Protocol;
using System.Text;
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

    public bool IsConnected => _connection is not null;

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> DiscoverDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selector = BluetoothDevice.GetDeviceSelector();

        var devices =
            await DeviceInformation.FindAllAsync(selector);

        var result =
            new List<BluetoothDeviceInfo>();

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bluetoothDevice =
                await BluetoothDevice.FromIdAsync(
                    device.Id);

            if (bluetoothDevice is null)
                continue;

            var rfcommResult =
                await bluetoothDevice.GetRfcommServicesAsync();

            var isTimeSyncDevice =
                rfcommResult.Services.Any(
                    service =>
                        service.ServiceId.Uuid ==
                        AppConstants.BluetoothServiceUuid);

            result.Add(
                new BluetoothDeviceInfo
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

    public async Task<IReadOnlyList<BluetoothRfcommServiceInfo>>
        GetRfcommServicesAsync(
            BluetoothDeviceInfo deviceInfo,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bluetoothDevice =
            await BluetoothDevice.FromIdAsync(
                deviceInfo.DeviceId);

        if (bluetoothDevice is null)
            return Array.Empty<BluetoothRfcommServiceInfo>();

        var result =
            await bluetoothDevice.GetRfcommServicesAsync();

        var services =
            new List<BluetoothRfcommServiceInfo>();

        foreach (var service in result.Services)
        {
            services.Add(
                new BluetoothRfcommServiceInfo
                {
                    ServiceUuid =
                        service.ServiceId.Uuid,

                    ConnectionServiceName =
                        service.ConnectionServiceName
                });
        }

        return services;
    }

    public async Task ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await DisconnectAsync();

        using var bluetoothDevice =
            await BluetoothDevice.FromIdAsync(
                device.DeviceId);

        if (bluetoothDevice is null)
        {
            throw new InvalidOperationException(
                "无法打开蓝牙设备。");
        }

        var serviceResult =
            await bluetoothDevice
                .GetRfcommServicesForIdAsync(
                    RfcommServiceId.FromUuid(
                        AppConstants.BluetoothServiceUuid));

        var service =
            serviceResult.Services.FirstOrDefault();

        if (service is null)
        {
            throw new InvalidOperationException(
                "目标设备未提供 BTTimeSync RFCOMM 服务。");
        }

        var socket =
            new StreamSocket();

        var reader =
            new DataReader(
                socket.InputStream)
            {
                ByteOrder = ByteOrder.BigEndian,
                InputStreamOptions =
                    InputStreamOptions.Partial
            };

        var writer =
            new DataWriter(
                socket.OutputStream)
            {
                ByteOrder = ByteOrder.BigEndian
            };

        try
        {
            await socket.ConnectAsync(
                service.ConnectionHostName,
                service.ConnectionServiceName);

            _connection =
                new BluetoothConnection
                {
                    Device = device,
                    Socket = socket,
                    Reader = reader,
                    Writer = writer,
                    ConnectedAtUtc = DateTime.UtcNow
                };

            await PerformHandshakeAsync(
                cancellationToken);

            service.Dispose();
        }
        catch
        {
            reader.Dispose();
            writer.Dispose();
            socket.Dispose();
            service.Dispose();
            _connection = null;

            throw;
        }
    }

    public Task DisconnectAsync()
    {
        _connection?.Dispose();

        _connection = null;

        return Task.CompletedTask;
    }

    private async Task PerformHandshakeAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var helloPayload =
            Encoding.UTF8.GetBytes(
                AppConstants.BluetoothServiceName);

        var helloPacket =
            new Packet
            {
                Version = 1,
                Type = PacketType.Hello,
                Payload = helloPayload
            };

        await SendPacketAsync(
            helloPacket,
            cancellationToken);

        var response =
            await ReceivePacketAsync(
                cancellationToken);

        if (response.Version != 1)
        {
            throw new IOException(
                $"HelloAck 协议版本错误：{response.Version}");
        }

        if (response.Type != PacketType.HelloAck)
        {
            throw new IOException(
                $"HelloAck 类型错误：{response.Type}");
        }

        if (response.Payload.Length != 0)
        {
            throw new IOException(
                "HelloAck Payload 长度错误。");
        }
    }

    public async Task SendBytesAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connection is null)
        {
            throw new InvalidOperationException(
                "当前没有已建立的蓝牙连接。");
        }

        _connection.Writer.WriteBytes(data);

        await _connection.Writer.StoreAsync();

        await _connection.Writer.FlushAsync();
    }

    public async Task<byte[]> ReceiveBytesAsync(
        int length,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_connection is null)
        {
            throw new InvalidOperationException(
                "当前没有已建立的蓝牙连接。");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length));
        }

        if (length == 0)
            return [];

        var result =
            new byte[length];

        var offset = 0;

        while (offset < result.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var available =
                _connection.Reader.UnconsumedBufferLength;

            if (available == 0)
            {
                var remaining =
                    result.Length - offset;

                var requestLength =
                    (uint)Math.Min(
                        remaining,
                        uint.MaxValue);

                var loaded =
                    await _connection.Reader.LoadAsync(
                        requestLength);

                if (loaded == 0)
                {
                    throw new IOException(
                        "蓝牙连接已关闭。");
                }

                available =
                    _connection.Reader.UnconsumedBufferLength;
            }

            var toRead =
                (int)Math.Min(
                    available,
                    (uint)(result.Length - offset));

            var buffer =
                new byte[toRead];

            _connection.Reader.ReadBytes(buffer);

            System.Buffer.BlockCopy(
                buffer,
                0,
                result,
                offset,
                toRead);

            offset += toRead;
        }

        return result;
    }

    public async Task SendPacketAsync(
        Packet packet,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data =
            PacketWriter.Encode(packet);

        await SendBytesAsync(
            data,
            cancellationToken);
    }

    public async Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const int headerLength = 6;

        var header =
            await ReceiveBytesAsync(
                headerLength,
                cancellationToken);

        var startOfFrame =
            System.Buffers.Binary.BinaryPrimitives
                .ReadUInt16BigEndian(
                    header.AsSpan(0, 2));

        if (startOfFrame != Packet.StartOfFrame)
        {
            throw new IOException(
                $"无效的 BTSP SOF：0x{startOfFrame:X4}");
        }

        var payloadLength =
            System.Buffers.Binary.BinaryPrimitives
                .ReadUInt16BigEndian(
                    header.AsSpan(4, 2));

        var remainingLength =
            checked(
                (int)payloadLength + 2);

        var remaining =
            await ReceiveBytesAsync(
                remainingLength,
                cancellationToken);

        var packetData =
            new byte[
                checked(
                    headerLength +
                    remainingLength)];

        System.Buffer.BlockCopy(
            header,
            0,
            packetData,
            0,
            headerLength);

        System.Buffer.BlockCopy(
            remaining,
            0,
            packetData,
            headerLength,
            remainingLength);

        return PacketReader.Decode(packetData);
    }

    private static string FormatBluetoothAddress(
        ulong address)
    {
        return address
            .ToString("X12")
            .Insert(2, ":")
            .Insert(5, ":")
            .Insert(8, ":")
            .Insert(11, ":")
            .Insert(14, ":");
    }
}