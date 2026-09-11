using BTTimeSync.Core.Protocol;

namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// BTSP 数据包传输接口。
/// </summary>
public interface IPacketTransport
{
    Task SendPacketAsync(
        Packet packet,
        CancellationToken cancellationToken = default);

    Task<Packet> ReceivePacketAsync(
        CancellationToken cancellationToken = default);
}