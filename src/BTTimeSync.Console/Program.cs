using System.ComponentModel;
using System.Runtime.InteropServices;
using BTTimeSync.Common;
using BTTimeSync.Core;
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
                // v0.6.1
                // NTP-style 四时间戳 + Median / MAD 异常值检测
                //
                // v0.6.1 相比 v0.6.0：
                // 1. SyncResult 保存 T1/T2/T3/T4
                // 2. 最终校时目标明确使用：
                //       T4 + FinalOffset
                // 3. 校时后的误差验证也使用：
                //       实际时间 - (T4 + FinalOffset)
                // ============================================================

                const int sampleCount = 10;

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "BTTimeSync v0.6.1 多次采样校时");
                System.Console.WriteLine(
                    "NTP-style 四时间戳");
                System.Console.WriteLine(
                    "Median / MAD 异常值检测");
                System.Console.WriteLine(
                    "校时后误差验证修正");
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
                // 找出 Delay 最小的样本
                //
                // 保留该结果用于与 v0.5.0 对比。
                // v0.6.1 实际校时仍然使用统计得到的 FinalOffset。
                // ============================================================

                var bestDelayResult =
                    syncResults
                        .OrderBy(x =>
                            x.RoundTripMilliseconds)
                        .First();

                var bestDelayIndex =
                    syncResults.IndexOf(bestDelayResult) + 1;

                // ============================================================
                // v0.6.1 Median / MAD 统计分析
                // ============================================================

                var offsets =
                    syncResults
                        .Select(x =>
                            x.OffsetMilliseconds)
                        .ToArray();

                var statistics =
                    TimeSyncStatistics.Analyze(
                        offsets);

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "          v0.6.1 统计分析");
                System.Console.WriteLine(
                    "========================================");

                System.Console.WriteLine(
                    $"原始有效样本：{syncResults.Count}");

                System.Console.WriteLine(
                    $"Offset Median：" +
                    $" {statistics.Median:+0.00;-0.00;0.00} ms");

                System.Console.WriteLine(
                    $"MAD：" +
                    $" {statistics.Mad:F2} ms");

                if (statistics.Mad > double.Epsilon)
                {
                    System.Console.WriteLine(
                        $"异常判断阈值：" +
                        $" ±{statistics.Threshold:F2} ms");
                }
                else
                {
                    System.Console.WriteLine(
                        "异常判断阈值：无（MAD 接近 0）");
                }

                System.Console.WriteLine(
                    $"正常样本：" +
                    $" {statistics.ValidIndexes.Count}");

                System.Console.WriteLine(
                    $"异常样本：" +
                    $" {statistics.OutlierIndexes.Count}");

                // ============================================================
                // 输出异常样本
                // ============================================================

                if (statistics.OutlierIndexes.Count > 0)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine(
                        "异常样本：");

                    foreach (var index in statistics.OutlierIndexes)
                    {
                        var resultItem =
                            syncResults[index];

                        System.Console.WriteLine(
                            $"第 {index + 1} 次：" +
                            $" Delay = " +
                            $"{resultItem.RoundTripMilliseconds:F1} ms，" +
                            $" Offset = " +
                            $"{resultItem.OffsetMilliseconds:+0.0;-0.0;0.0} ms");
                    }
                }
                else
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine(
                        "未检测到异常样本。");
                }

                // ============================================================
                // 输出正常样本
                // ============================================================

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "正常样本：");

                foreach (var index in statistics.ValidIndexes)
                {
                    var resultItem =
                        syncResults[index];

                    System.Console.WriteLine(
                        $"第 {index + 1} 次：" +
                        $" Delay = " +
                        $"{resultItem.RoundTripMilliseconds:F1} ms，" +
                        $" Offset = " +
                        $"{resultItem.OffsetMilliseconds:+0.0;-0.0;0.0} ms");
                }

                // ============================================================
                // 最终校时结果
                // ============================================================

                var finalOffset =
                    statistics.FinalOffset;

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "             校时结果对比");
                System.Console.WriteLine(
                    "========================================");

                System.Console.WriteLine(
                    $"最佳 Delay 样本：第 {bestDelayIndex} 次");

                System.Console.WriteLine(
                    $"最佳 Delay：" +
                    $" {bestDelayResult.RoundTripMilliseconds:F1} ms");

                System.Console.WriteLine(
                    $"最佳 Delay 样本 Offset：" +
                    $" {bestDelayResult.OffsetMilliseconds:+0.0;-0.0;0.0} ms");

                System.Console.WriteLine(
                    $"Median：" +
                    $" {statistics.Median:+0.00;-0.00;0.00} ms");

                System.Console.WriteLine(
                    $"最终 Offset：" +
                    $" {finalOffset:+0.00;-0.00;0.00} ms");

                // ============================================================
                // 选择最终校时参考样本
                //
                // 只从正常样本中选择 Delay 最小的样本。
                //
                // 注意：
                // 参考样本只提供 T4；
                // 真正的 Offset 使用 statistics.FinalOffset。
                // ============================================================

                var validResults =
                    statistics.ValidIndexes
                        .Select(index => new
                        {
                            Index = index,
                            Result = syncResults[index]
                        })
                        .ToList();

                if (validResults.Count == 0)
                {
                    throw new InvalidOperationException(
                        "异常值剔除后没有可用样本，无法进行校时。");
                }

                var referenceSample =
                    validResults
                        .OrderBy(x =>
                            x.Result.RoundTripMilliseconds)
                        .First();

                var referenceIndex =
                    referenceSample.Index + 1;

                var referenceResult =
                    referenceSample.Result;

                // ============================================================
                // v0.6.1 最终校时目标
                //
                // 正确公式：
                //
                //     Target = T4 + FinalOffset
                //
                // T4：
                //     客户端收到服务器 TimeResponse 的本地 UTC
                //
                // FinalOffset：
                //     Median/MAD 统计得到的最终时钟偏差
                // ============================================================

                var targetUnixMilliseconds =
                    referenceResult.T4 +
                    (long)Math.Round(
                        finalOffset);

                var finalTargetTime =
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            targetUnixMilliseconds);

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");
                System.Console.WriteLine(
                    "             最终校时");
                System.Console.WriteLine(
                    "========================================");

                System.Console.WriteLine(
                    $"参考样本：第 {referenceIndex} 次");

                System.Console.WriteLine(
                    $"参考 Delay：" +
                    $" {referenceResult.RoundTripMilliseconds:F1} ms");

                System.Console.WriteLine(
                    $"参考样本 T4：" +
                    $" {referenceResult.T4}");

                System.Console.WriteLine(
                    $"参考样本远端 UTC：" +
                    $" {referenceResult.RemoteTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine(
                    $"最终校时 Offset：" +
                    $" {finalOffset:+0.00;-0.00;0.00} ms");

                System.Console.WriteLine(
                    $"最终校时目标 UTC：" +
                    $" {finalTargetTime:yyyy-MM-dd HH:mm:ss.fff}");

                // ============================================================
                // 只在这里设置一次 Windows 系统时间
                // ============================================================

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "正在设置内网机系统时间...");

                var targetTime =
                    finalTargetTime;

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
                // v0.6.1 校时后验证
                //
                // 旧版错误公式：
                //
                //     CorrectedTime - RemoteTime - FinalOffset
                //
                // RemoteTime 是 T3，而校时目标是从 T4 出发计算的，
                // 因此两者不能直接这样比较。
                //
                // 正确验证：
                //
                //     理论校正时间 = T4 + FinalOffset
                //
                //     剩余误差 =
                //         |实际系统时间 - 理论校正时间|
                // ============================================================

                var correctedTime =
                    DateTimeOffset.UtcNow;

                var theoreticalCorrectedTime =
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            targetUnixMilliseconds);

                var remainingError =
                    Math.Abs(
                        (
                            correctedTime -
                            theoreticalCorrectedTime
                        ).TotalMilliseconds);

                System.Console.WriteLine(
                    $"校时后 UTC：" +
                    $" {correctedTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine(
                    $"理论校正 UTC：" +
                    $" {theoreticalCorrectedTime:yyyy-MM-dd HH:mm:ss.fff}");

                System.Console.WriteLine(
                    $"校时后相对理论目标的剩余误差：" +
                    $" {remainingError:F0} ms");

                System.Console.WriteLine();
                System.Console.WriteLine(
                    "========================================");

                if (remainingError <= 50)
                {
                    System.Console.WriteLine(
                        "BTTimeSync v0.6.1 校时成功！");
                }
                else
                {
                    System.Console.WriteLine(
                        "BTTimeSync v0.6.1 校时完成，" +
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
    // NTP-style 四时间戳
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
    // 本方法只负责测量一次，不负责修改系统时间。
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
            // T4：客户端收到 TimeResponse 后立即记录
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
            // TimeResponse Payload：
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
            // 解析服务器返回的 T1
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
            // 本次样本的目标时间
            //
            // Target = T4 + Offset
            //
            // 这里只用于保存当前样本的理论目标。
            // 最终校时仍使用统计得到的 FinalOffset。
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
            //
            // v0.6.1：同时保存 T1/T2/T3/T4，
            // 供最终校时和误差验证使用。
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
                    targetTime,

                T1 = t1,
                T2 = t2,
                T3 = t3,
                T4 = t4
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