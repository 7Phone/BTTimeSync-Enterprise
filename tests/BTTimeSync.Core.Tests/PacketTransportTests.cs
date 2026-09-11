using BTTimeSync.Core.Protocol;
using BTTimeSync.Core.Services;

namespace BTTimeSync.Core.Tests;

public sealed class PacketTransportTests
{
    [Fact]
    public async Task SendPacketAsync_WritesEncodedPacket()
    {
        var transport =
            new FakeByteTransport();

        var packetTransport =
            new PacketTransport(
                transport);

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
                        3
                    }
            };

        await packetTransport.SendPacketAsync(
            packet);

        Assert.Single(
            transport.SentData);

        var decoded =
            PacketReader.Decode(
                transport.SentData[0]);

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
    public async Task ReceivePacketAsync_ReadsCompletePacket()
    {
        var fakeTransport =
            new FakeByteTransport();

        var packetTransport =
            new PacketTransport(
                fakeTransport);

        var expected =
            new Packet
            {
                Version = 1,
                Type = PacketType.HelloAck,
                Payload = Array.Empty<byte>()
            };

        fakeTransport.QueueReceivedData(
            PacketWriter.Encode(
                expected));

        var actual =
            await packetTransport.ReceivePacketAsync();

        Assert.Equal(
            expected.Version,
            actual.Version);

        Assert.Equal(
            expected.Type,
            actual.Type);

        Assert.Equal(
            expected.Payload,
            actual.Payload);
    }

    [Fact]
    public async Task ReceivePacketAsync_WithPayload_ReadsPayload()
    {
        var fakeTransport =
            new FakeByteTransport();

        var packetTransport =
            new PacketTransport(
                fakeTransport);

        var expected =
            new Packet
            {
                Version = 1,
                Type = PacketType.RequestTime,
                Payload =
                    new byte[]
                    {
                        0x10,
                        0x20,
                        0x30,
                        0x40,
                        0x50
                    }
            };

        fakeTransport.QueueReceivedData(
            PacketWriter.Encode(
                expected));

        var actual =
            await packetTransport.ReceivePacketAsync();

        Assert.Equal(
            expected.Version,
            actual.Version);

        Assert.Equal(
            expected.Type,
            actual.Type);

        Assert.Equal(
            expected.Payload,
            actual.Payload);
    }

    [Fact]
    public async Task ReceivePacketAsync_WithInvalidSof_Throws()
    {
        var fakeTransport =
            new FakeByteTransport();

        var packetTransport =
            new PacketTransport(
                fakeTransport);

        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.Hello,
                Payload = Array.Empty<byte>()
            };

        var data =
            PacketWriter.Encode(
                packet);

        data[0] ^= 0xFF;

        fakeTransport.QueueReceivedData(
            data);

        await Assert.ThrowsAsync<IOException>(
            () =>
                packetTransport
                    .ReceivePacketAsync());
    }

    [Fact]
    public async Task ReceivePacketAsync_WhenTransportIsShort_Throws()
    {
        var fakeTransport =
            new FakeByteTransport();

        var packetTransport =
            new PacketTransport(
                fakeTransport);

        fakeTransport.QueueReceivedData(
            new byte[]
            {
                0x42,
                0x54,
                0x01
            });

        await Assert.ThrowsAsync<IOException>(
            () =>
                packetTransport
                    .ReceivePacketAsync());
    }
}