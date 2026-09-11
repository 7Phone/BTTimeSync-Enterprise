using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Protocol;
using System.Text;

namespace BTTimeSync.Core.Services;

/// <summary>
/// BTSP 会话服务。
/// </summary>
public sealed class BtspSession : IBtspSession
{
    private const byte ProtocolVersion = 1;

    private readonly IPacketTransport _packetTransport;

    public BtspSession(
        IPacketTransport packetTransport)
    {
        _packetTransport =
            packetTransport ??
            throw new ArgumentNullException(
                nameof(packetTransport));
    }

    public async Task HandshakeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var helloPayload =
            Encoding.UTF8.GetBytes(
                BTTimeSync.Common.AppConstants
                    .BluetoothServiceName);

        var helloPacket =
            new Packet
            {
                Version = ProtocolVersion,
                Type = PacketType.Hello,
                Payload = helloPayload
            };

        await _packetTransport.SendPacketAsync(
            helloPacket,
            cancellationToken);

        var response =
            await _packetTransport.ReceivePacketAsync(
                cancellationToken);

        ValidateHelloAck(response);
    }

    private static void ValidateHelloAck(
        Packet response)
    {
        if (response.Version != ProtocolVersion)
        {
            throw new IOException(
                $"HelloAck 协议版本错误：{response.Version}");
        }

        if (response.Type != PacketType.HelloAck)
        {
            throw new IOException(
                $"HelloAck 类型错误：{response.Type}");
        }

        if (response.Payload.Length != 0)
        {
            throw new IOException(
                "HelloAck Payload 长度错误。");
        }
    }
}