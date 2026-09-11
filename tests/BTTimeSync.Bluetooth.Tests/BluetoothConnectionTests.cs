using BTTimeSync.Bluetooth.Models;

namespace BTTimeSync.Bluetooth.Tests;

public sealed class BluetoothConnectionTests
{
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var connection =
            new BluetoothConnection
            {
                Device = null!,
                Socket = null!,
                Reader = null!,
                Writer = null!
            };

        connection.Dispose();
        connection.Dispose();
    }
}