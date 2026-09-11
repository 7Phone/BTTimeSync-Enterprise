using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Common;
using BTTimeSync.Common.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BTTimeSync.Bluetooth.Services;

/// <summary>
/// Windows RFCOMM 蓝牙连接服务。
/// </summary>
public sealed class BluetoothConnector : IBluetoothConnector
{
    /// <inheritdoc />
    public async Task<BluetoothConnection> ConnectAsync(
        BluetoothDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            foreach (var item in serviceResult.Services)
            {
                item.Dispose();
            }

            throw new InvalidOperationException(
                "目标设备未提供 BTTimeSync RFCOMM 服务。");
        }

        var socket =
            new StreamSocket();

        var reader =
            new DataReader(
                socket.InputStream)
            {
                ByteOrder =
                    ByteOrder.BigEndian,

                InputStreamOptions =
                    InputStreamOptions.Partial
            };

        var writer =
            new DataWriter(
                socket.OutputStream)
            {
                ByteOrder =
                    ByteOrder.BigEndian
            };

        try
        {
            await socket.ConnectAsync(
                service.ConnectionHostName,
                service.ConnectionServiceName);

            return new BluetoothConnection
            {
                Device = device,
                Socket = socket,
                Reader = reader,
                Writer = writer,
                ConnectedAtUtc =
                    DateTime.UtcNow
            };
        }
        catch
        {
            reader.Dispose();
            writer.Dispose();
            socket.Dispose();

            throw;
        }
        finally
        {
            service.Dispose();

            foreach (var item in serviceResult.Services)
            {
                if (!ReferenceEquals(item, service))
                {
                    item.Dispose();
                }
            }
        }
    }
}