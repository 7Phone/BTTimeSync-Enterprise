using BTTimeSync.Common;
using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Protocol;

namespace BTTimeSync.Core.Services;

/// <summary>
/// 时间同步服务。
/// </summary>
public sealed class TimeSyncService : ITimeSyncService
{
    private readonly IByteTransport _transport;

    public TimeSyncService(IByteTransport transport)
    {
        _transport =
            transport ??
            throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>
    /// 执行一次时间同步。
    /// </summary>
    public async Task<SyncResult> SyncOnceAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var t1 =
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();

        var payload =
            new byte[8];

        System.Buffers.Binary.BinaryPrimitives
            .WriteInt64BigEndian(
                payload,
                t1);

        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.RequestTime,
                Payload = payload
            };

        var encoded =
            PacketWriter.Encode(packet);

        await _transport.SendBytesAsync(
            encoded,
            cancellationToken);

        var response =
            await ReceivePacketAsync(
                cancellationToken);

        var t4 =
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();

        if (response.Version != 1)
        {
            throw new IOException(
                $"TimeResponse 协议版本错误：{response.Version}");
        }

        if (response.Type != PacketType.TimeResponse)
        {
            throw new IOException(
                $"TimeResponse 类型错误：{response.Type}");
        }

        if (response.Payload.Length != 24)
        {
            throw new IOException(
                $"TimeResponse Payload 长度错误：{response.Payload.Length}");
        }

        var responseT1 =
            System.Buffers.Binary.BinaryPrimitives
                .ReadInt64BigEndian(
                    response.Payload.AsSpan(
                        0,
                        8));

        var t2 =
            System.Buffers.Binary.BinaryPrimitives
                .ReadInt64BigEndian(
                    response.Payload.AsSpan(
                        8,
                        8));

        var t3 =
            System.Buffers.Binary.BinaryPrimitives
                .ReadInt64BigEndian(
                    response.Payload.AsSpan(
                        16,
                        8));

        if (responseT1 != t1)
        {
            throw new IOException(
                "TimeResponse 中的 T1 与请求不一致。");
        }

        var delay =
            (t4 - t1) -
            (t3 - t2);

        var offset =
            (
                (t2 - t1) +
                (t3 - t4)
            ) / 2.0;

        var remoteTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(
                    t3);

        var targetTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(
                    t4)
                .AddMilliseconds(
                    offset);

        return new SyncResult
        {
            Success = true,
            RoundTripMilliseconds = delay,
            OffsetMilliseconds = offset,
            RemoteUnixMilliseconds = t3,
            RemoteTime = remoteTime,
            TargetTime = targetTime,
            T1 = t1,
            T2 = t2,
            T3 = t3,
            T4 = t4
        };
    }

    /// <summary>
    /// 接收一个完整 BTSP 数据包。
    /// </summary>
    private async Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken)
    {
        const int HeaderLength = 6;

        var header =
            await _transport.ReceiveBytesAsync(
                HeaderLength,
                cancellationToken);

        var startOfFrame =
            System.Buffers.Binary.BinaryPrimitives
                .ReadUInt16BigEndian(
                    header.AsSpan(
                        0,
                        2));

        if (startOfFrame != Packet.StartOfFrame)
        {
            throw new IOException(
                $"无效的 BTSP SOF：0x{startOfFrame:X4}");
        }

        var payloadLength =
            System.Buffers.Binary.BinaryPrimitives
                .ReadUInt16BigEndian(
                    header.AsSpan(
                        4,
                        2));

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

        System.Buffer.BlockCopy(
            header,
            0,
            packetData,
            0,
            HeaderLength);

        System.Buffer.BlockCopy(
            remaining,
            0,
            packetData,
            HeaderLength,
            remainingLength);

        return PacketReader.Decode(packetData);
    }
}