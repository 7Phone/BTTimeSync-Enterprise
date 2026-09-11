using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;

namespace BTTimeSync.Bluetooth.Services;

/// <summary>
/// Windows RFCOMM 字节传输服务。
/// </summary>
public sealed class BluetoothTransport : IBluetoothTransport
{
    private BluetoothConnection? _connection;

    public bool IsConnected =>
        _connection is not null;

    /// <summary>
    /// 设置当前连接。
    /// </summary>
    public void Attach(
        BluetoothConnection connection)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        Disconnect();

        _connection = connection;
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

        ArgumentNullException.ThrowIfNull(data);

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
                _connection.Reader
                    .UnconsumedBufferLength;

            if (available == 0)
            {
                var remaining =
                    result.Length - offset;

                var requestLength =
                    (uint)Math.Min(
                        remaining,
                        uint.MaxValue);

                var loaded =
                    await _connection.Reader
                        .LoadAsync(
                            requestLength);

                if (loaded == 0)
                {
                    throw new IOException(
                        "蓝牙连接已关闭。");
                }

                available =
                    _connection.Reader
                        .UnconsumedBufferLength;
            }

            var toRead =
                (int)Math.Min(
                    available,
                    (uint)(result.Length - offset));

            var buffer =
                new byte[toRead];

            _connection.Reader
                .ReadBytes(buffer);

            Buffer.BlockCopy(
                buffer,
                0,
                result,
                offset,
                toRead);

            offset += toRead;
        }

        return result;
    }

    public Task DisconnectAsync()
    {
        Disconnect();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Disconnect();
    }

    private void Disconnect()
    {
        var connection =
            Interlocked.Exchange(
                ref _connection,
                null);

        connection?.Dispose();
    }
}