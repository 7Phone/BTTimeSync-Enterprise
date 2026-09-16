using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Services;
using BTTimeSync.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BTTimeSync.Bluetooth;

public static class DependencyInjection
{
    public static IServiceCollection AddBluetooth(
        this IServiceCollection services)
    {
        services.AddSingleton<BluetoothTransport>();

        services.AddSingleton<
            IBluetoothTransport>(
            sp =>
                sp.GetRequiredService<BluetoothTransport>());


        services.AddSingleton<
            IByteTransport>(
            sp =>
                sp.GetRequiredService<BluetoothTransport>());


        services.AddSingleton<IBluetoothService>(
            sp =>
                new BluetoothService(
                    new BluetoothDiscovery(),
                    new BluetoothConnector(),
                    sp.GetRequiredService<
                        IBluetoothTransport>()));


        return services;
    }
}