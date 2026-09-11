using BTTimeSync.Core.Interfaces;

namespace BTTimeSync.Core.Tests;

internal sealed class FakeByteTransport : IByteTransport
{
    private readonly Queue<byte> _receiveBuffer = new();

    private readonly List<byte[]> _sentData = new();

    public IReadOnlyList<byte[]> SentData =>
        _sentData;

    public void QueueReceivedData(
        byte[] data)
    {
        ArgumentNullException.ThrowIfNull(
            data);

        foreach (var value in data)
        {
            _receiveBuffer.Enqueue(
                value);
        }
    }

    public Task SendBytesAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(
            data);

        _sentData.Add(
            data.ToArray());

        return Task.CompletedTask;
    }

    public Task<byte[]> ReceiveBytesAsync(
        int length,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length));
        }

        if (_receiveBuffer.Count < length)
        {
            throw new IOException(
                $"Fake Transport 数据不足。" +
                $"需要 {length} 字节，" +
                $"实际只有 {_receiveBuffer.Count} 字节。");
        }

        var result =
            new byte[length];

        for (var i = 0;
             i < length;
             i++)
        {
            result[i] =
                _receiveBuffer.Dequeue();
        }

        return Task.FromResult(
            result);
    }
}