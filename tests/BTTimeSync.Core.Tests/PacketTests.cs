using BTTimeSync.Core.Protocol;

namespace BTTimeSync.Core.Tests;

public sealed class PacketTests
{
    [Fact]
    public void PacketWriter_AndPacketReader_RoundTrip()
    {
        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.Hello,
                Payload =
                    new byte[]
                    {
                        0x42,
                        0x54,
                        0x53,
                        0x50
                    }
            };

        var encoded =
            PacketWriter.Encode(
                packet);

        var decoded =
            PacketReader.Decode(
                encoded);

        Assert.Equal(
            packet.Version,
            decoded.Version);

        Assert.Equal(
            packet.Type,
            decoded.Type);

        Assert.Equal(
            packet.Payload,
            decoded.Payload);
    }

    [Fact]
    public void PacketWriter_AndPacketReader_HandleEmptyPayload()
    {
        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.HelloAck,
                Payload = Array.Empty<byte>()
            };

        var encoded =
            PacketWriter.Encode(
                packet);

        var decoded =
            PacketReader.Decode(
                encoded);

        Assert.Equal(
            packet.Version,
            decoded.Version);

        Assert.Equal(
            packet.Type,
            decoded.Type);

        Assert.Empty(
            decoded.Payload);
    }

    [Fact]
    public void PacketWriter_AndPacketReader_HandleLargePayload()
    {
        var payload =
            Enumerable
                .Range(0, 255)
                .Select(
                    x => (byte)x)
                .ToArray();

        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.RequestTime,
                Payload = payload
            };

        var encoded =
            PacketWriter.Encode(
                packet);

        var decoded =
            PacketReader.Decode(
                encoded);

        Assert.Equal(
            payload,
            decoded.Payload);
    }

    [Fact]
    public void PacketReader_WithInvalidSof_Throws()
    {
        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.Hello,
                Payload = Array.Empty<byte>()
            };

        var encoded =
            PacketWriter.Encode(
                packet);

        encoded[0] ^= 0xFF;

        Assert.Throws<ArgumentException>(
            () =>
                PacketReader.Decode(
                    encoded));
    }

    [Fact]
    public void PacketReader_WithTruncatedPacket_Throws()
    {
        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.Hello,
                Payload =
                    new byte[]
                    {
                        1,
                        2,
                        3,
                        4
                    }
            };

        var encoded =
            PacketWriter.Encode(
                packet);

        var truncated =
            encoded[..^1];

        Assert.Throws<ArgumentException>(
            () =>
                PacketReader.Decode(
                    truncated));
    }
}