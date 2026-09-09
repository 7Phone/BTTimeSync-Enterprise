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

                // ============================================================
                // BTSP Hello 握手
                // ============================================================

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

                // ============================================================
                // v0.5.0 NTP-style 四时间戳多次采样校时
                // ============================================================

                const int sampleCount = 10;

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "BTTimeSync v0.5.0 多次采样校时");
                System.Console.WriteLine(
                    "NTP-style 四时间戳");
                System.Console.WriteLine(
                    $"计划采样次数：{sampleCount}");
                System.Console.WriteLine(
                    "========================================");

                var syncResults =
                    new List<SyncResult>();

                for (var i = 1; i <= sampleCount; i++)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine(
                        $"========== 第{i}次采样 ==========");

                    var syncResult =
                        await SyncOnceAsync(
                            writer,
                            reader);

                    if (syncResult.Success)
                    {
                        syncResults.Add(syncResult);

                        System.Console.WriteLine();
                        System.Console.WriteLine(
                            $"本次采样结果：Delay = " +
                            $"{syncResult.RoundTripMilliseconds:F1} ms，" +
                            $"时间偏差 = " +
                            $"{syncResult.OffsetMilliseconds:+0.0;-0.0;0.0} ms");
                    }
                    else
                    {
                        System.Console.WriteLine();
                        System.Console.WriteLine(
                            "本次采样失败！");

                        System.Console.WriteLine(
                            $"原因：{syncResult.ErrorMessage}");
                    }

                    if (i < sampleCount)
                    {
                        await Task.Delay(100);
                    }
                }

                if (syncResults.Count == 0)
                {
                    throw new InvalidOperationException(
                        "10 次采样全部失败，无法进行校时。");
                }

                // ============================================================
                // 输出全部采样结果
                // ============================================================

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "           多次采样结果汇总");
                System.Console.WriteLine(
                    "========================================");

                for (var i = 0; i < syncResults.Count; i++)
                {
                    var resultItem =
                        syncResults[i];

                    System.Console.WriteLine(
                        $"样本 {i + 1,2}：" +
                        $"Delay = {resultItem.RoundTripMilliseconds,6:F1} ms，" +
                        $"偏差 = " +
                        $"{resultItem.OffsetMilliseconds,7:+0.0;-0.0;0.0} ms");
                }

                // ============================================================
                // 选择 Delay 最小的样本
                // ============================================================

                var bestResult =
                    syncResults
                        .OrderBy(x =>
                            x.RoundTripMilliseconds)
                        .First();

                var bestIndex =
                    syncResults.IndexOf(bestResult) + 1;

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "             最佳样本");
                System.Console.WriteLine(
                    "========================================");

                System.Console.WriteLine(
                    $"最佳样本：第 {bestIndex} 次");

                System.Console.WriteLine(
                    $"最佳 Delay：" +
                    $" {bestResult.RoundTripMilliseconds:F1} ms");

                System.Console.WriteLine(
                    $"时间偏差：" +
                    $" {bestResult.OffsetMilliseconds:+0.0;-0.0;0.0} ms");

                System.Console.WriteLine(
                    $"远端 UTC：" +
                    $" {bestResult.RemoteTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine(
                    $"校时目标 UTC：" +
                    $" {bestResult.TargetTime:yyyy-MM-dd HH:mm:ss.fff}");

                // ============================================================
                // 只在这里设置一次 Windows 系统时间
                // ============================================================

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "正在设置内网机系统时间...");

                var targetTime =
                    bestResult.TargetTime;

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

                systemTime.wDayOfWeek =
                    (ushort)targetTime.DayOfWeek;

                if (!SetSystemTime(ref systemTime))
                {
                    var errorCode =
                        Marshal.GetLastWin32Error();

                    throw new Win32Exception(
                        errorCode,
                        "设置 Windows 系统时间失败。");
                }

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "系统时间设置成功！");

                // ============================================================
                // 校时后验证
                // ============================================================

                var correctedTime =
                    DateTimeOffset.UtcNow;

                var remainingError =
                    Math.Abs(
                        (correctedTime -
                         bestResult.RemoteTime)
                        .TotalMilliseconds);

                System.Console.WriteLine(
                    $"校时后 UTC：" +
                    $" {correctedTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine(
                    $"校时后剩余误差：" +
                    $" {remainingError:F0} ms");

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");

                if (remainingError <= 50)
                {
                    System.Console.WriteLine(
                        "BTTimeSync v0.5.0 校时成功！");
                }
                else
                {
                    System.Console.WriteLine(
                        "BTTimeSync v0.5.0 校时完成，" +
                        "但剩余误差超过 50 ms。");
                }

                System.Console.WriteLine(
                    "========================================");

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "按 Enter 退出。");

                System.Console.ReadLine();

                return;
            }

            System.Console.WriteLine(
                "未找到 BTTimeSync RFCOMM 服务.");

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

    // ========================================================================
    // 单次校时
    //
    // v0.5.0：NTP-style 四时间戳
    //
    // T1：客户端发送 TimeRequest
    // T2：服务器收到 TimeRequest
    // T3：服务器发送 TimeResponse
    // T4：客户端收到 TimeResponse
    //
    // Delay  = (T4 - T1) - (T3 - T2)
    // Offset = ((T2 - T1) + (T3 - T4)) / 2
    //
    // Offset > 0：
    //     服务器时间领先客户端
    //
    // 本方法只负责“测量一次”，不负责修改系统时间。
    // ========================================================================

    private static async Task<SyncResult> SyncOnceAsync(
        DataWriter writer,
        DataReader reader)
    {
        try
        {
            // ------------------------------------------------------------
            // T1：客户端发送 TimeRequest 前记录 UTC 时间
            // ------------------------------------------------------------

            var t1 =
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // ------------------------------------------------------------
            // 创建 TimeRequest
            //
            // Payload：
            // T1 = 8 字节，大端序
            // ------------------------------------------------------------

            var requestPayload = new byte[8];

            System.Buffers.Binary.BinaryPrimitives
                .WriteInt64BigEndian(
                    requestPayload,
                    t1);

            var requestPacket = new Packet
            {
                Version = 1,
                Type = PacketType.RequestTime,
                Payload = requestPayload
            };

            var requestData =
                PacketWriter.Encode(requestPacket);

            // ------------------------------------------------------------
            // 发送 TimeRequest
            // ------------------------------------------------------------

            writer.WriteBytes(requestData);

            await writer.StoreAsync();
            await writer.FlushAsync();

            System.Console.WriteLine();
            System.Console.WriteLine(
                "已发送 BTSP RequestTime！");
            System.Console.WriteLine(
                $"T1 客户端发送：{t1}");
            System.Console.WriteLine(
                $"HEX：{BitConverter.ToString(requestData)}");

            // ------------------------------------------------------------
            // 等待 TimeResponse
            // ------------------------------------------------------------

            var response =
                await ReceivePacketAsync(reader);

            // ------------------------------------------------------------
            // T4：客户端收到 TimeResponse 后记录
            // ------------------------------------------------------------

            var t4 =
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // ------------------------------------------------------------
            // 显示 TimeResponse
            // ------------------------------------------------------------

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
                $"Payload : {BitConverter.ToString(response.Payload)}");
            System.Console.WriteLine(
                $"CRC16   : 0x{response.Crc16:X4}");

            // ------------------------------------------------------------
            // 验证响应
            //
            // v0.5.0 TimeResponse Payload：
            //
            // T1 = 8 字节
            // T2 = 8 字节
            // T3 = 8 字节
            //
            // 共 24 字节
            // ------------------------------------------------------------

            if (response.Type != PacketType.TimeResponse ||
                response.Payload.Length != 24)
            {
                throw new InvalidOperationException(
                    "收到的 TimeResponse 数据格式错误，" +
                    "Payload 应为 24 字节 T1/T2/T3。");
            }

            // ------------------------------------------------------------
            // 解析 T1
            // ------------------------------------------------------------

            var t1FromServer =
                System.Buffers.Binary.BinaryPrimitives
                    .ReadInt64BigEndian(
                        response.Payload.AsSpan(0, 8));

            // ------------------------------------------------------------
            // 解析 T2
            // ------------------------------------------------------------

            var t2 =
                System.Buffers.Binary.BinaryPrimitives
                    .ReadInt64BigEndian(
                        response.Payload.AsSpan(8, 8));

            // ------------------------------------------------------------
            // 解析 T3
            // ------------------------------------------------------------

            var t3 =
                System.Buffers.Binary.BinaryPrimitives
                    .ReadInt64BigEndian(
                        response.Payload.AsSpan(16, 8));

            // ------------------------------------------------------------
            // 验证服务器返回的 T1
            //
            // 防止请求/响应错配。
            // ------------------------------------------------------------

            if (t1FromServer != t1)
            {
                throw new InvalidOperationException(
                    $"TimeResponse 中的 T1 与本次请求不一致。" +
                    $" 请求 T1={t1}，响应 T1={t1FromServer}。");
            }

            // ------------------------------------------------------------
            // 创建四时间戳对象
            // ------------------------------------------------------------

            var timestamps =
                new TimeSyncTimestamps
                {
                    T1 = t1,
                    T2 = t2,
                    T3 = t3,
                    T4 = t4
                };

            // ------------------------------------------------------------
            // 显示四个时间戳
            // ------------------------------------------------------------

            System.Console.WriteLine();
            System.Console.WriteLine(
                "NTP-style 四时间戳：");
            System.Console.WriteLine(
                $"T1 客户端发送：{timestamps.T1}");
            System.Console.WriteLine(
                $"T2 服务器接收：{timestamps.T2}");
            System.Console.WriteLine(
                $"T3 服务器发送：{timestamps.T3}");
            System.Console.WriteLine(
                $"T4 客户端接收：{timestamps.T4}");

            // ------------------------------------------------------------
            // 计算 Delay
            //
            // Delay = (T4 - T1) - (T3 - T2)
            // ------------------------------------------------------------

            var delayMilliseconds =
                (timestamps.T4 - timestamps.T1) -
                (timestamps.T3 - timestamps.T2);

            // ------------------------------------------------------------
            // 计算 Offset
            //
            // Offset = ((T2 - T1) + (T3 - T4)) / 2
            //
            // Offset > 0：
            //     服务器时间领先客户端
            // ------------------------------------------------------------

            var offsetMilliseconds =
                (
                    (timestamps.T2 - timestamps.T1) +
                    (timestamps.T3 - timestamps.T4)
                ) / 2.0;

            // ------------------------------------------------------------
            // 防御异常情况
            // ------------------------------------------------------------

            if (delayMilliseconds < 0)
            {
                throw new InvalidOperationException(
                    $"计算出的网络延迟异常：{delayMilliseconds} ms。");
            }

            // ------------------------------------------------------------
            // 远端 UTC
            //
            // T3 是服务器发送 TimeResponse 时的 UTC。
            // ------------------------------------------------------------

            var remoteTime =
                DateTimeOffset
                    .FromUnixTimeMilliseconds(
                        t3);

            // ------------------------------------------------------------
            // 计算校时目标
            //
            // 目标时间 = T4 对应的客户端时间 + Offset
            //
            // 这里仅计算目标时间，不修改系统时间。
            // ------------------------------------------------------------

            var targetUnixMilliseconds =
                t4 +
                (long)Math.Round(
                    offsetMilliseconds);

            var targetTime =
                DateTimeOffset
                    .FromUnixTimeMilliseconds(
                        targetUnixMilliseconds);

            // ------------------------------------------------------------
            // 显示计算结果
            // ------------------------------------------------------------

            System.Console.WriteLine();
            System.Console.WriteLine(
                $"网络 Delay：{delayMilliseconds:F1} ms");
            System.Console.WriteLine(
                $"时间 Offset：{offsetMilliseconds:+0.0;-0.0;0.0} ms");

            // ------------------------------------------------------------
            // 返回本次采样结果
            // ------------------------------------------------------------

            return new SyncResult
            {
                Success = true,

                RoundTripMilliseconds =
                    delayMilliseconds,

                OffsetMilliseconds =
                    offsetMilliseconds,

                RemoteUnixMilliseconds =
                    t3,

                RemoteTime =
                    remoteTime,

                TargetTime =
                    targetTime
            };
        }
        catch (Exception ex)
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ========================================================================
    // 接收完整 BTSP 数据包
    // ========================================================================

    private static async Task<Packet> ReceivePacketAsync(
        DataReader reader)
    {
        // ------------------------------------------------------------
        // BTSP 固定头：
        //
        // 55 AA
        // Version
        // Type
        // Length High
        // Length Low
        //
        // 共 6 字节
        // ------------------------------------------------------------

        await LoadExactlyAsync(
            reader,
            6);

        var header = new byte[6];

        reader.ReadBytes(header);

        // ------------------------------------------------------------
        // 读取 Payload Length
        // ------------------------------------------------------------

        var payloadLength =
            (header[4] << 8) |
            header[5];

        // ------------------------------------------------------------
        // 剩余部分：
        //
        // Payload
        // CRC16
        //
        // = Payload Length + 2
        // ------------------------------------------------------------

        var remainingLength =
            payloadLength + 2;

        await LoadExactlyAsync(
            reader,
            (uint)remainingLength);

        var remaining =
            new byte[remainingLength];

        reader.ReadBytes(remaining);

        // ------------------------------------------------------------
        // 合并完整数据包
        // ------------------------------------------------------------

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

        // ------------------------------------------------------------
        // 交给 PacketReader 解析
        // ------------------------------------------------------------

        return PacketReader.Decode(packetData);
    }

    // ========================================================================
    // 确保 DataReader 中读取到指定长度的数据
    // ========================================================================

    private static async Task LoadExactlyAsync(
        DataReader reader,
        uint requiredLength)
    {
        while (reader.UnconsumedBufferLength <
               requiredLength)
        {
            var missingLength =
                requiredLength -
                reader.UnconsumedBufferLength;

            var loaded =
                await reader.LoadAsync(
                    missingLength);

            if (loaded == 0)
            {
                throw new InvalidOperationException(
                    "蓝牙连接已关闭，未能读取完整的 BTSP 数据包。");
            }
        }
    }
}