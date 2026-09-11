using BTTimeSync.Common;
using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Protocol;
using System.Buffers.Binary;

namespace BTTimeSync.Core.Services;

/// <summary>
/// 单次时间同步服务。
/// </summary>
public sealed class TimeSyncService : ITimeSyncService
{
    private const byte ProtocolVersion = 1;
    private const int RequestPayloadLength = 8;
    private const int ResponsePayloadLength = 24;

    private readonly IPacketTransport _packetTransport;

    public TimeSyncService(
        IPacketTransport packetTransport)
    {
        _packetTransport =
            packetTransport ??
            throw new ArgumentNullException(
                nameof(packetTransport));
    }

    /// <inheritdoc />
    public async Task<SyncResult> SyncOnceAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var t1 =
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();

        var requestPayload =
            new byte[RequestPayloadLength];

        BinaryPrimitives.WriteInt64BigEndian(
            requestPayload,
            t1);

        var requestPacket =
            new Packet
            {
                Version = ProtocolVersion,
                Type = PacketType.RequestTime,
                Payload = requestPayload
            };

        await _packetTransport.SendPacketAsync(
            requestPacket,
            cancellationToken);

        var response =
            await _packetTransport.ReceivePacketAsync(
                cancellationToken);

        var t4 =
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();

        ValidateResponse(response);

        var responseT1 =
            BinaryPrimitives.ReadInt64BigEndian(
                response.Payload.AsSpan(
                    0,
                    8));

        var t2 =
            BinaryPrimitives.ReadInt64BigEndian(
                response.Payload.AsSpan(
                    8,
                    8));

        var t3 =
            BinaryPrimitives.ReadInt64BigEndian(
                response.Payload.AsSpan(
                    16,
                    8));

        if (responseT1 != t1)
        {
            throw new IOException(
                "TimeResponse 中的 T1 与请求不一致。");
        }

        var roundTripMilliseconds =
            (t4 - t1) -
            (t3 - t2);

        var offsetMilliseconds =
            (
                (t2 - t1) +
                (t3 - t4)
            ) / 2.0;

        var remoteTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(t3);

        var targetTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(t4)
                .AddMilliseconds(
                    offsetMilliseconds);

        return new SyncResult
        {
            Success = true,
            RoundTripMilliseconds =
                roundTripMilliseconds,
            OffsetMilliseconds =
                offsetMilliseconds,
            RemoteUnixMilliseconds =
                t3,
            RemoteTime =
                remoteTime,
            TargetTime =
                targetTime,
            T1 = t1,
            T2 = t2,
            T3 = t3,
            T4 = t4
        };
    }

    private static void ValidateResponse(
        Packet response)
    {
        if (response.Version != ProtocolVersion)
        {
            throw new IOException(
                $"TimeResponse 协议版本错误：{response.Version}");
        }

        if (response.Type != PacketType.TimeResponse)
        {
            throw new IOException(
                $"TimeResponse 类型错误：{response.Type}");
        }

        if (response.Payload.Length !=
            ResponsePayloadLength)
        {
            throw new IOException(
                $"TimeResponse Payload 长度错误：{response.Payload.Length}");
        }
    }
}