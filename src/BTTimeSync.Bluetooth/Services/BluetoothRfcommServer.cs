using BTTimeSync.Common;
using BTTimeSync.Core.Protocol;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BTTimeSync.Bluetooth.Services;

/// <summary>
/// BTTimeSync RFCOMM 服务端。
/// 负责发布 RFCOMM 服务、广播 SDP 信息并处理客户端通信。
/// </summary>
public sealed class BluetoothRfcommServer : IDisposable
{
    private RfcommServiceProvider? _provider;
    private StreamSocketListener? _listener;
    private StreamSocket? _clientSocket;

    public bool IsAdvertising { get; private set; }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _provider =
            await RfcommServiceProvider.CreateAsync(
                RfcommServiceId.FromUuid(
                    AppConstants.BluetoothServiceUuid));

        cancellationToken.ThrowIfCancellationRequested();

        var sdpWriter = new DataWriter();

        sdpWriter.WriteByte(0x25);
        sdpWriter.WriteByte(
            (byte)AppConstants.BluetoothServiceName.Length);

        sdpWriter.UnicodeEncoding =
            UnicodeEncoding.Utf8;

        sdpWriter.WriteString(
            AppConstants.BluetoothServiceName);

        _provider.SdpRawAttributes.Add(
            0x0100,
            sdpWriter.DetachBuffer());

        cancellationToken.ThrowIfCancellationRequested();

        _listener = new StreamSocketListener();

        _listener.ConnectionReceived +=
            OnConnectionReceived;

        await _listener.BindServiceNameAsync(
            _provider.ServiceId.AsString(),
            SocketProtectionLevel
                .BluetoothEncryptionAllowNullAuthentication);

        cancellationToken.ThrowIfCancellationRequested();

        _provider.StartAdvertising(
            _listener,
            true);

        IsAdvertising = true;
    }

    public void Stop()
    {
        if (_provider is not null)
        {
            try
            {
                _provider.StopAdvertising();
            }
            catch
            {
            }
        }

        _clientSocket?.Dispose();
        _clientSocket = null;

        _listener?.Dispose();
        _listener = null;

        _provider = null;

        IsAdvertising = false;
    }

    private void OnConnectionReceived(
        StreamSocketListener sender,
        StreamSocketListenerConnectionReceivedEventArgs args)
    {
        _clientSocket?.Dispose();

        _clientSocket = args.Socket;

        System.Console.WriteLine();
        System.Console.WriteLine(
            "========================================");
        System.Console.WriteLine(
            "内网机 RFCOMM 客户端已连接！");
        System.Console.WriteLine(
            $"连接时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        System.Console.WriteLine(
            "========================================");
        System.Console.WriteLine();

        _ = ReceivePacketAsync(_clientSocket);
    }

    private static async Task ReceivePacketAsync(
        StreamSocket socket)
    {
        try
        {
            using var reader =
                new DataReader(socket.InputStream);

            reader.InputStreamOptions =
                InputStreamOptions.Partial;

            System.Console.WriteLine(
                "等待 BTSP 数据包...");

            while (true)
            {
                var loaded =
                    await reader.LoadAsync(6);

                if (loaded == 0)
                {
                    System.Console.WriteLine(
                        "客户端已断开连接。");

                    return;
                }

                var header = new byte[6];

                reader.ReadBytes(header);

                var payloadLength =
                    (header[4] << 8) |
                    header[5];

                var remainingLength =
                    payloadLength + 2;

                await LoadExactlyAsync(
                    reader,
                    (uint)remainingLength);

                var remaining =
                    new byte[remainingLength];

                reader.ReadBytes(remaining);

                var packetData =
                    new byte[6 + remainingLength];

                System.Buffer.BlockCopy(
                    header,
                    0,
                    packetData,
                    0,
                    header.Length);

                System.Buffer.BlockCopy(
                    remaining,
                    0,
                    packetData,
                    6,
                    remaining.Length);

                var packet =
                    PacketReader.Decode(packetData);

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "收到 BTSP 数据包！");
                System.Console.WriteLine(
                    $"Version : {packet.Version}");
                System.Console.WriteLine(
                    $"Type    : {packet.Type}");
                System.Console.WriteLine(
                    $"Length  : {packet.Length}");
                System.Console.WriteLine(
                    $"Payload : {BitConverter.ToString(packet.Payload)}");
                System.Console.WriteLine(
                    $"CRC16   : 0x{packet.Crc16:X4}");

                if (packet.Type == PacketType.Hello)
                {
                    await SendHelloAckAsync(socket);
                }
                else if (packet.Type == PacketType.RequestTime)
                {
                    await SendTimeResponseAsync(socket);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine();
            System.Console.WriteLine(
                "BTSP 数据接收失败！");
            System.Console.WriteLine(
                $"异常类型：{ex.GetType().FullName}");
            System.Console.WriteLine(
                $"异常信息：{ex.Message}");
        }
    }

    private static async Task SendHelloAckAsync(
        StreamSocket socket)
    {
        var packet = new Packet
        {
            Version = 1,
            Type = PacketType.HelloAck,
            Payload = []
        };

        var packetData =
            PacketWriter.Encode(packet);

        using var writer =
            new DataWriter(socket.OutputStream);

        writer.WriteBytes(packetData);

        await writer.StoreAsync();
        await writer.FlushAsync();

        writer.DetachStream();

        System.Console.WriteLine();
        System.Console.WriteLine(
            "已发送 BTSP HelloAck！");
        System.Console.WriteLine(
            $"HEX：{BitConverter.ToString(packetData)}");
    }

    private static async Task SendTimeResponseAsync(
        StreamSocket socket)
    {
        var unixMilliseconds =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var payload = new byte[8];

        System.Buffers.Binary.BinaryPrimitives
            .WriteInt64BigEndian(
                payload,
                unixMilliseconds);

        var packet = new Packet
        {
            Version = 1,
            Type = PacketType.TimeResponse,
            Payload = payload
        };

        var packetData =
            PacketWriter.Encode(packet);

        using var writer =
            new DataWriter(socket.OutputStream);

        writer.WriteBytes(packetData);

        await writer.StoreAsync();
        await writer.FlushAsync();

        writer.DetachStream();

        System.Console.WriteLine();
        System.Console.WriteLine(
            "已发送 BTSP TimeResponse！");
        System.Console.WriteLine(
            $"UTC时间：{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
        System.Console.WriteLine(
            $"Unix毫秒：{unixMilliseconds}");
        System.Console.WriteLine(
            $"HEX：{BitConverter.ToString(packetData)}");
    }

    private static async Task LoadExactlyAsync(
        DataReader reader,
        uint requiredLength)
    {
        while (reader.UnconsumedBufferLength < requiredLength)
        {
            var missingLength =
                requiredLength -
                reader.UnconsumedBufferLength;

            var loaded =
                await reader.LoadAsync(missingLength);

            if (loaded == 0)
            {
                throw new InvalidOperationException(
                    "蓝牙连接已关闭，未能读取完整的 BTSP 数据包。");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}