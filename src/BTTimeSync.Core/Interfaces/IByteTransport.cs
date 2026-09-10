namespace BTTimeSync.Core.Interfaces;

/// <summary>
/// 底层字节数据传输接口。
/// </summary>
/// <remarks>
/// Core 层只依赖字节收发，不关心具体通信方式。
/// 具体实现可以是 Windows RFCOMM、串口、TCP 等。
/// </remarks>
public interface IByteTransport
{
    /// <summary>
    /// 发送完整字节数据。
    /// </summary>
    /// <param name="data">要发送的数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SendBytesAsync(
        byte[] data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 接收指定长度的完整字节数据。
    /// </summary>
    /// <param name="length">需要接收的字节数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>指定长度的字节数据。</returns>
    Task<byte[]> ReceiveBytesAsync(
        int length,
        CancellationToken cancellationToken = default);
}