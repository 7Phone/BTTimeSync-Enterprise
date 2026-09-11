using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Models;

namespace BTTimeSync.Bluetooth.Tests;

internal sealed class FakeBluetoothTransport : IBluetoothTransport
{
    private readonly Queue<byte> _receiveBuffer = new();

    public bool IsConnected { get; private set; }

    public readonly List<byte[]> SentData = new();

    public BluetoothConnection? AttachedConnection { get; private set; }

    public void Attach(BluetoothConnection connection)
    {
        AttachedConnection = connection;
        IsConnected = true;
    }

    public void QueueReceive(byte[] data)
    {
        foreach (var b in data)
        {
            _receiveBuffer.Enqueue(b);
        }
    }

    public Task SendBytesAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        SentData.Add(data.ToArray());
        return Task.CompletedTask;
    }

    public Task<byte[]> ReceiveBytesAsync(
        int length,
        CancellationToken cancellationToken = default)
    {
        if (_receiveBuffer.Count < length)
            throw new IOException("FakeBluetoothTransport 数据不足。");

        var result = new byte[length];

        for (int i = 0; i < length; i++)
            result[i] = _receiveBuffer.Dequeue();

        return Task.FromResult(result);
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsConnected = false;
    }
}