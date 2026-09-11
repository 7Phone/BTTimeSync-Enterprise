using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Protocol;
using System.Buffers.Binary;

namespace BTTimeSync.Core.Services;

/// <summary>
/// 基于字节传输的 BTSP 数据包传输实现。
/// </summary>
public sealed class PacketTransport : IPacketTransport
{
    private const int HeaderLength = 6;

    private readonly IByteTransport _transport;

    public PacketTransport(
        IByteTransport transport)
    {
        _transport =
            transport ??
            throw new ArgumentNullException(
                nameof(transport));
    }

    public async Task SendPacketAsync(
        Packet packet,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data =
            PacketWriter.Encode(packet);

        await _transport.SendBytesAsync(
            data,
            cancellationToken);
    }

    public async Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var header =
            await _transport.ReceiveBytesAsync(
                HeaderLength,
                cancellationToken);

        var startOfFrame =
            BinaryPrimitives.ReadUInt16BigEndian(
                header.AsSpan(0, 2));

        if (startOfFrame != Packet.StartOfFrame)
        {
            throw new IOException(
                $"无效的 BTSP SOF：0x{startOfFrame:X4}");
        }

        var payloadLength =
            BinaryPrimitives.ReadUInt16BigEndian(
                header.AsSpan(4, 2));

        var remainingLength =
            checked(
                (int)payloadLength + 2);

        var remaining =
            await _transport.ReceiveBytesAsync(
                remainingLength,
                cancellationToken);

        var packetData =
            new byte[
                checked(
                    HeaderLength +
                    remainingLength)];

        Buffer.BlockCopy(
            header,
            0,
            packetData,
            0,
            HeaderLength);

        Buffer.BlockCopy(
            remaining,
            0,
            packetData,
            HeaderLength,
            remainingLength);

        return PacketReader.Decode(
            packetData);
    }
}