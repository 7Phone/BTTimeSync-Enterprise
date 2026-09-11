namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// BTSP 会话服务。
/// </summary>
public interface IBtspSession
{
    Task HandshakeAsync(
        CancellationToken cancellationToken = default);
}