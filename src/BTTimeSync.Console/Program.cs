using System.ComponentModel;
using System.Runtime.InteropServices;
using BTTimeSync.Common;
using BTTimeSync.Core.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace BTTimeSync.Console;

internal class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemTime(
        ref SYSTEMTIME lpSystemTime);

    static async Task Main(string[] args)
    {
        System.Console.WriteLine("BTTimeSync RFCOMM Client");
        System.Console.WriteLine("========================");
        System.Console.WriteLine();

        System.Console.WriteLine(
            $"目标服务 UUID：{AppConstants.BluetoothServiceUuid}");

        System.Console.WriteLine();

        try
        {
            var selector =
                BluetoothDevice.GetDeviceSelector();

            var devices =
                await DeviceInformation.FindAllAsync(selector);

            System.Console.WriteLine(
                $"发现蓝牙设备：{devices.Count} 个");

            System.Console.WriteLine();

            foreach (var device in devices)
            {
                System.Console.WriteLine(
                    $"设备：{device.Name}");

                using var bluetoothDevice =
                    await BluetoothDevice.FromIdAsync(
                        device.Id);

                if (bluetoothDevice is null)
                {
                    continue;
                }

                System.Console.WriteLine(
                    "正在查询 BTTimeSync RFCOMM 服务...");

                var result =
                    await bluetoothDevice
                        .GetRfcommServicesForIdAsync(
                            RfcommServiceId.FromUuid(
                                AppConstants.BluetoothServiceUuid),
                            BluetoothCacheMode.Uncached);

                System.Console.WriteLine(
                    $"匹配服务数量：{result.Services.Count}");

                var service =
                    result.Services.FirstOrDefault();

                if (service is null)
                {
                    continue;
                }

                System.Console.WriteLine(
                    $"服务 UUID：{service.ServiceId.Uuid}");

                System.Console.WriteLine(
                    "正在建立 RFCOMM 连接...");

                using var socket =
                    new StreamSocket();

                await socket.ConnectAsync(
                    service.ConnectionHostName,
                    service.ConnectionServiceName,
                    SocketProtectionLevel
                        .BluetoothEncryptionAllowNullAuthentication);

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "RFCOMM 连接成功！");

                using var writer =
                    new DataWriter(socket.OutputStream);

                using var reader =
                    new DataReader(socket.InputStream);

                reader.InputStreamOptions =
                    InputStreamOptions.Partial;

                // ====================================================
                // BTSP Hello 握手
                // ====================================================

                var helloPayload =
                    System.Text.Encoding.UTF8.GetBytes(
                        AppConstants.BluetoothServiceName);

                var helloPacket = new Packet
                {
                    Version = 1,
                    Type = PacketType.Hello,
                    Payload = helloPayload
                };

                var helloData =
                    PacketWriter.Encode(helloPacket);

                writer.WriteBytes(helloData);

                await writer.StoreAsync();
                await writer.FlushAsync();

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "已发送 BTSP Hello！");
                System.Console.WriteLine(
                    $"HEX：{BitConverter.ToString(helloData)}");

                // ====================================================
                // 接收 HelloAck
                // ====================================================

                var helloAck =
                    await ReceivePacketAsync(reader);

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "收到 BTSP HelloAck！");
                System.Console.WriteLine(
                    $"Version : {helloAck.Version}");
                System.Console.WriteLine(
                    $"Type    : {helloAck.Type}");
                System.Console.WriteLine(
                    $"Length  : {helloAck.Length}");
                System.Console.WriteLine(
                    $"Payload : {BitConverter.ToString(helloAck.Payload)}");
                System.Console.WriteLine(
                    $"CRC16   : 0x{helloAck.Crc16:X4}");

                if (helloAck.Type != PacketType.HelloAck ||
                    helloAck.Payload.Length != 0)
                {
                    throw new InvalidOperationException(
                        "收到的 HelloAck 数据格式错误。");
                }

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "BTSP Hello 握手成功！");

                // ====================================================
                // 记录内网机发送前时间
                // ====================================================

                var localTimeBefore =
                    DateTimeOffset.UtcNow;

                var localUnixMillisecondsBefore =
                    localTimeBefore.ToUnixTimeMilliseconds();

                // ====================================================
                // 发送 RequestTime
                // ====================================================

                var requestPacket = new Packet
                {
                    Version = 1,
                    Type = PacketType.RequestTime,
                    Payload = []
                };

                var requestData =
                    PacketWriter.Encode(requestPacket);

                writer.WriteBytes(requestData);

                await writer.StoreAsync();
                await writer.FlushAsync();

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "已发送 BTSP RequestTime！");
                System.Console.WriteLine(
                    $"HEX：{BitConverter.ToString(requestData)}");

                // ====================================================
                // 接收 TimeResponse
                // ====================================================

                var response =
                    await ReceivePacketAsync(reader);

                // ====================================================
                // 记录内网机收到响应后的时间
                // ====================================================

                var localTimeAfter =
                    DateTimeOffset.UtcNow;

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "收到 BTSP TimeResponse！");
                System.Console.WriteLine(
                    $"Version : {response.Version}");
                System.Console.WriteLine(
                    $"Type    : {response.Type}");
                System.Console.WriteLine(
                    $"Length  : {response.Length}");
                System.Console.WriteLine(
                    $"CRC16   : 0x{response.Crc16:X4}");

                if (response.Type != PacketType.TimeResponse ||
                    response.Payload.Length != 8)
                {
                    throw new InvalidOperationException(
                        "收到的 TimeResponse 数据格式错误。");
                }

                var remoteUnixMilliseconds =
                    System.Buffers.Binary.BinaryPrimitives
                        .ReadInt64BigEndian(
                            response.Payload);

                var remoteTime =
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            remoteUnixMilliseconds);

                // ====================================================
                // 计算校时目标时间
                // ====================================================

                var localUnixMillisecondsAfter =
                    localTimeAfter.ToUnixTimeMilliseconds();

                var localMidpointMilliseconds =
                    (localUnixMillisecondsBefore +
                     localUnixMillisecondsAfter) / 2.0;

                var offsetMilliseconds =
                    remoteUnixMilliseconds -
                    localMidpointMilliseconds;

                var targetUnixMilliseconds =
                    localUnixMillisecondsAfter +
                    (long)Math.Round(offsetMilliseconds);

                var targetTime =
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            targetUnixMilliseconds);

                var roundTripMilliseconds =
                    localUnixMillisecondsAfter -
                    localUnixMillisecondsBefore;

                System.Console.WriteLine();
                System.Console.WriteLine(
                    $"内网机当前 UTC：{localTimeAfter:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine(
                    $"外网机 UTC：{remoteTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========== 校时计算 ==========");

                System.Console.WriteLine(
                    $"往返耗时：{roundTripMilliseconds:F1} ms");

                System.Console.WriteLine(
                    $"时间偏差：{offsetMilliseconds:F1} ms");

                System.Console.WriteLine(
                    $"校时目标 UTC：{targetTime:yyyy-MM-dd HH:mm:ss.fff}");

                // ====================================================
                // 设置内网机系统时间
                // ====================================================

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "正在设置内网机系统时间...");

                var systemTime = new SYSTEMTIME
                {
                    wYear = (ushort)targetTime.Year,
                    wMonth = (ushort)targetTime.Month,
                    wDay = (ushort)targetTime.Day,
                    wHour = (ushort)targetTime.Hour,
                    wMinute = (ushort)targetTime.Minute,
                    wSecond = (ushort)targetTime.Second,
                    wMilliseconds =
                        (ushort)targetTime.Millisecond
                };

                if (!SetSystemTime(ref systemTime))
                {
                    var errorCode =
                        Marshal.GetLastWin32Error();

                    throw new Win32Exception(
                        errorCode,
                        $"设置系统时间失败，Windows 错误代码：{errorCode}");
                }

                var correctedTime =
                    DateTimeOffset.UtcNow;

                System.Console.WriteLine(
                    "系统时间设置成功！");

                System.Console.WriteLine(
                    $"校时后 UTC：{correctedTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine();

                var remainingError =
                    correctedTime.ToUnixTimeMilliseconds() -
                    remoteUnixMilliseconds;

                System.Console.WriteLine(
                    $"校时后剩余误差：{remainingError} ms");

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "BTTimeSync 系统校时成功！");

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "按任意键退出...");

                System.Console.ReadKey();

                return;
            }

            System.Console.WriteLine(
                "未找到 BTTimeSync RFCOMM 服务。");

            System.Console.ReadKey();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine();
            System.Console.WriteLine(
                "通信或校时失败！");
            System.Console.WriteLine(
                $"异常类型：{ex.GetType().FullName}");
            System.Console.WriteLine(
                $"异常信息：{ex.Message}");

            if (ex.InnerException is not null)
            {
                System.Console.WriteLine(
                    $"内部异常：{ex.InnerException.Message}");
            }

            System.Console.WriteLine();
            System.Console.WriteLine(
                "按任意键退出...");

            System.Console.ReadKey();
        }
    }

    private static async Task<Packet> ReceivePacketAsync(
        DataReader reader)
    {
        await LoadExactlyAsync(
            reader,
            6);

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

        return PacketReader.Decode(packetData);
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
}