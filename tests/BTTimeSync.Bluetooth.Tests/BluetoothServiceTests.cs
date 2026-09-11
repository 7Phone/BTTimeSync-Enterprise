using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;
using BTTimeSync.Bluetooth.Services;
using BTTimeSync.Common.Models;

namespace BTTimeSync.Bluetooth.Tests;

public sealed class BluetoothServiceTests
{
    [Fact]
    public async Task Disconnect_WhenNotConnected_DoesNotThrow()
    {
        var service =
            CreateService(
                new FakeBluetoothTransport());

        await service.DisconnectAsync();
    }

    [Fact]
    public async Task Disconnect_CallsTransportDisconnect()
    {
        var transport =
            new FakeBluetoothTransport();

        transport.Attach(
            new BluetoothConnection
            {
                Device = null!,
                Socket = null!,
                Reader = null!,
                Writer = null!
            });

        var service =
            CreateService(
                transport);

        await service.DisconnectAsync();

        Assert.False(
            transport.IsConnected);
    }

    private static BluetoothService CreateService(
        FakeBluetoothTransport transport)
    {
        return new BluetoothService(
            new FakeDiscovery(),
            new FakeConnector(),
            transport);
    }

    private sealed class FakeDiscovery : IBluetoothDiscovery
    {
        public Task<IReadOnlyList<BluetoothDeviceInfo>> DiscoverDevicesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BluetoothDeviceInfo>>(
                Array.Empty<BluetoothDeviceInfo>());
        }

        public Task<IReadOnlyList<BluetoothRfcommServiceInfo>> GetRfcommServicesAsync(
            BluetoothDeviceInfo device,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BluetoothRfcommServiceInfo>>(
                Array.Empty<BluetoothRfcommServiceInfo>());
        }
    }

    private sealed class FakeConnector : IBluetoothConnector
    {
        public Task<BluetoothConnection> ConnectAsync(
            BluetoothDeviceInfo device,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new BluetoothConnection
                {
                    Device = null!,
                    Socket = null!,
                    Reader = null!,
                    Writer = null!
                });
        }
    }
}