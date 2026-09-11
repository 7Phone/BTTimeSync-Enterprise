using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Common;
using BTTimeSync.Common.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BTTimeSync.Bluetooth.Services;

/// <summary>
/// Windows 蓝牙设备发现服务。
/// </summary>
public sealed class BluetoothDiscovery : IBluetoothDiscovery
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<BluetoothDeviceInfo>>
        DiscoverDevicesAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selector =
            BluetoothDevice.GetDeviceSelector();

        var devices =
            await DeviceInformation.FindAllAsync(
                selector);

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
                await bluetoothDevice
                    .GetRfcommServicesAsync();

            try
            {
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
                        Address =
                            FormatBluetoothAddress(
                                bluetoothDevice.BluetoothAddress),
                        IsPaired =
                            device.Pairing.IsPaired,
                        IsTimeSyncDevice =
                            isTimeSyncDevice,
                        LastSeenUtc =
                            DateTime.UtcNow
                    });
            }
            finally
            {
                foreach (var service in
                         rfcommResult.Services)
                {
                    service.Dispose();
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
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
            await bluetoothDevice
                .GetRfcommServicesAsync();

        try
        {
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
        finally
        {
            foreach (var service in result.Services)
            {
                service.Dispose();
            }
        }
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