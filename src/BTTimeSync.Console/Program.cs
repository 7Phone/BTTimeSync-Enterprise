using BTTimeSync.Common;
using BTTimeSync.Core;
using BTTimeSync.Core.Protocol;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

internal class Program
{
    private const int SampleCount = 10;
    private const int SampleIntervalMilliseconds = 100;

    private const int SyncIntervalMinutes = 30;

    private const double VerificationThresholdMilliseconds = 50.0;

    private const int ReconnectRetryIntervalSeconds = 5;
    private const int ReconnectRetryIntervalMaximumSeconds = 30;

    private static volatile bool _shutdownRequested;

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
    private static extern bool SetSystemTime(ref SYSTEMTIME st);

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("BTTimeSync v0.7.1");
        Console.WriteLine("================");
        Console.WriteLine();

        Console.CancelKeyPress += OnCancelKeyPress;

        ConnectionContext? connection = null;

        try
        {
            connection = await ConnectAndHandshakeAsync();

            while (!_shutdownRequested)
            {
                if (!IsConnectionUsable(connection))
                {
                    DisposeConnection(connection);
                    connection = await ReconnectAsync();

                    if (_shutdownRequested)
                        break;
                }

                var syncResult =
                    await RunSyncCycleAsync(connection);

                if (_shutdownRequested)
                    break;

                if (syncResult.ConnectionLost)
                {
                    Console.WriteLine();
                    Console.WriteLine("检测到蓝牙连接断开。");
                    Console.WriteLine("准备自动重新连接...");

                    DisposeConnection(connection);
                    connection = await ReconnectAsync();

                    if (_shutdownRequested)
                        break;

                    continue;
                }

                await WaitForNextSyncAsync(
                    TimeSpan.FromMinutes(SyncIntervalMinutes));
            }
        }
        catch (OperationCanceledException)
            when (_shutdownRequested)
        {
            Console.WriteLine();
            Console.WriteLine("收到退出请求。");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("程序发生未处理异常：");
            Console.WriteLine(ex);
        }
        finally
        {
            DisposeConnection(connection);

            Console.WriteLine();
            Console.WriteLine("BTTimeSync 已退出。");
        }
    }

    private static void OnCancelKeyPress(
        object? sender,
        ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _shutdownRequested = true;

        Console.WriteLine();
        Console.WriteLine("正在退出 BTTimeSync...");
    }

    private static async Task<ConnectionContext>
        ConnectAndHandshakeAsync()
    {
        while (!_shutdownRequested)
        {
            try
            {
                return await ConnectAndHandshakeOnceAsync();
            }
            catch (Exception ex)
                when (IsConnectionException(ex))
            {
                Console.WriteLine();
                Console.WriteLine($"连接失败：{ex.Message}");

                if (_shutdownRequested)
                    break;

                Console.WriteLine(
                    $"将在 {ReconnectRetryIntervalSeconds} 秒后重试...");

                await DelayWithShutdownAsync(
                    TimeSpan.FromSeconds(
                        ReconnectRetryIntervalSeconds));
            }
        }

        throw new OperationCanceledException();
    }

    private static async Task<ConnectionContext>
        ConnectAndHandshakeOnceAsync()
    {
        Console.WriteLine("正在搜索 BTTimeSync 蓝牙设备...");

        var selector =
            BluetoothDevice.GetDeviceSelector();

        var devices =
            await DeviceInformation.FindAllAsync(selector);

        BluetoothDevice? targetDevice = null;
        RfcommDeviceService? targetService = null;

        foreach (var deviceInformation in devices)
        {
            if (_shutdownRequested)
                throw new OperationCanceledException();

            BluetoothDevice? bluetoothDevice = null;

            try
            {
                bluetoothDevice =
                    await BluetoothDevice.FromIdAsync(
                        deviceInformation.Id);

                if (bluetoothDevice is null)
                    continue;

                var deviceName =
                    bluetoothDevice.Name;

                if (string.IsNullOrWhiteSpace(deviceName))
                    deviceName = deviceInformation.Name;

                Console.WriteLine(
                    $"发现设备：{deviceName}");

                var services =
                    await bluetoothDevice
                        .GetRfcommServicesForIdAsync(
                            RfcommServiceId.FromUuid(
                                AppConstants.BluetoothServiceUuid));

                if (services.Services.Count == 0)
                    continue;

                targetDevice = bluetoothDevice;
                targetService = services.Services[0];

                Console.WriteLine(
                    "发现 BTTimeSync RFCOMM 服务。");

                break;
            }
            catch
            {
                bluetoothDevice?.Dispose();
            }
        }

        if (targetDevice is null ||
            targetService is null)
        {
            throw new IOException(
                "未找到 BTTimeSync RFCOMM 服务。");
        }

        var socket =
            new StreamSocket();

        var writer =
            new DataWriter(
                socket.OutputStream)
            {
                ByteOrder = ByteOrder.BigEndian
            };

        var reader =
            new DataReader(
                socket.InputStream)
            {
                ByteOrder = ByteOrder.BigEndian,
                InputStreamOptions =
                    InputStreamOptions.Partial
            };

        try
        {
            await socket.ConnectAsync(
                targetService.ConnectionHostName,
                targetService.ConnectionServiceName);

            Console.WriteLine(
                "RFCOMM 连接成功。");

            Console.WriteLine();

            var connection =
                new ConnectionContext
                {
                    Device = targetDevice,
                    Service = targetService,
                    Socket = socket,
                    Reader = reader,
                    Writer = writer
                };

            await PerformHandshakeAsync(connection);

            return connection;
        }
        catch
        {
            reader.Dispose();
            writer.Dispose();
            socket.Dispose();
            targetService.Dispose();
            targetDevice.Dispose();

            throw;
        }
    }

    private static async Task PerformHandshakeAsync(
        ConnectionContext connection)
    {
        var helloPayload =
            Encoding.UTF8.GetBytes(
                AppConstants.BluetoothServiceName);

        var helloPacket =
            new Packet
            {
                Version = 1,
                Type = PacketType.Hello,
                Payload = helloPayload
            };

        var encoded =
            PacketWriter.Encode(
                helloPacket);

        connection.Writer.WriteBytes(
            encoded);

        await connection.Writer.StoreAsync();
        await connection.Writer.FlushAsync();

        Console.WriteLine(
            "已发送 BTSP Hello。");

        var response =
            await ReceivePacketAsync(connection);

        if (response.Version != 1)
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

        Console.WriteLine(
            "BTSP Hello/HelloAck 握手成功。");
    }

    private static async Task<ConnectionContext>
        ReconnectAsync()
    {
        var retrySeconds =
            ReconnectRetryIntervalSeconds;

        while (!_shutdownRequested)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine(
                    "========== 自动重连 ==========");

                var connection =
                    await ConnectAndHandshakeOnceAsync();

                Console.WriteLine(
                    "自动重连成功。");

                Console.WriteLine(
                    "==============================");

                return connection;
            }
            catch (Exception ex)
                when (IsConnectionException(ex))
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"自动重连失败（{ex.GetType().Name}）：");

                Console.WriteLine(
                    $"HResult: 0x{ex.HResult:X8}");

                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    Console.WriteLine(ex.Message);
                }

                if (_shutdownRequested)
                    break;

                Console.WriteLine(
                    $"{retrySeconds} 秒后再次尝试...");

                await DelayWithShutdownAsync(
                    TimeSpan.FromSeconds(retrySeconds));

                retrySeconds =
                    Math.Min(
                        retrySeconds * 2,
                        ReconnectRetryIntervalMaximumSeconds);
            }
        }

        throw new OperationCanceledException();
    }

    private static async Task<SyncCycleResult>
        RunSyncCycleAsync(
            ConnectionContext connection)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"开始时间同步：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine(
            "========================================");

        var results =
            new List<SyncResult>();

        try
        {
            for (var i = 0;
                 i < SampleCount;
                 i++)
            {
                if (_shutdownRequested)
                {
                    return new SyncCycleResult
                    {
                        Success = false
                    };
                }

                var result =
                    await SyncOnceAsync(connection);

                results.Add(result);

                Console.WriteLine(
                    $"样本 #{i + 1:00}  " +
                    $"Delay={result.RoundTripMilliseconds,6:F1} ms  " +
                    $"Offset={result.OffsetMilliseconds,8:F1} ms");

                if (i < SampleCount - 1)
                {
                    await DelayWithShutdownAsync(
                        TimeSpan.FromMilliseconds(
                            SampleIntervalMilliseconds));
                }
            }
        }
        catch (Exception ex)
            when (IsConnectionException(ex))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"时间同步过程中连接断开：{ex.Message}");

            return new SyncCycleResult
            {
                Success = false,
                ConnectionLost = true
            };
        }

        if (results.Count == 0)
        {
            return new SyncCycleResult
            {
                Success = false
            };
        }

        var offsets =
            results
                .Select(x => x.OffsetMilliseconds)
                .ToArray();

        var statistics =
            TimeSyncStatistics.Analyze(offsets);

        Console.WriteLine();
        Console.WriteLine(
            "---------- 统计结果 ----------");

        Console.WriteLine(
            $"Median Offset : {statistics.Median:+0.00;-0.00;0.00} ms");

        Console.WriteLine(
            $"MAD           : {statistics.Mad:F2} ms");

        Console.WriteLine(
            $"Threshold     : ±{statistics.Threshold:F2} ms");

        Console.WriteLine(
            $"正常样本       : {statistics.ValidIndexes.Count}");

        Console.WriteLine(
            $"异常样本       : {statistics.OutlierIndexes.Count}");

        Console.WriteLine(
            $"Final Offset  : {statistics.FinalOffset:+0.00;-0.00;0.00} ms");

        var bestIndex =
            statistics.ValidIndexes
                .OrderBy(
                    index =>
                        results[index]
                            .RoundTripMilliseconds)
                .First();

        var bestResult =
            results[bestIndex];

        Console.WriteLine();
        Console.WriteLine(
            "---------- 最佳样本 ----------");

        Console.WriteLine(
            $"样本编号       : #{bestIndex + 1:00}");

        Console.WriteLine(
            $"最佳 Delay     : {bestResult.RoundTripMilliseconds:F1} ms");

        Console.WriteLine(
            $"对应 Offset    : {bestResult.OffsetMilliseconds:+0.00;-0.00;0.00} ms");

        var referenceResult =
            bestResult;

        var targetUnixMilliseconds =
            referenceResult.T4 +
            (long)Math.Round(
                statistics.FinalOffset);

        var theoreticalTargetTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(
                    targetUnixMilliseconds);

        Console.WriteLine();
        Console.WriteLine(
            $"参考 T4       : {referenceResult.T4}");

        Console.WriteLine(
            $"远程 UTC      : {referenceResult.RemoteTime:yyyy-MM-dd HH:mm:ss.fff}");

        Console.WriteLine(
            $"目标 UTC      : {theoreticalTargetTime:yyyy-MM-dd HH:mm:ss.fff}");

        Console.WriteLine(
            $"最终 Offset    : {statistics.FinalOffset:+0.00;-0.00;0.00} ms");

        /*
         * 校时验证：
         *
         * 不能直接使用：
         *
         *     DateTimeOffset.UtcNow - theoreticalTargetTime
         *
         * 因为 SetSystemTime() 返回后，
         * 程序自身执行也会消耗一定时间。
         *
         * 使用 Stopwatch 记录校时操作期间的
         * 单调时间，避免把程序执行耗时误判为
         * 校时误差。
         */

        var verificationStopwatch =
            Stopwatch.StartNew();

        SetSystemTimeFromUnixMilliseconds(
            targetUnixMilliseconds);

        verificationStopwatch.Stop();

        var elapsedAfterSetMilliseconds =
            verificationStopwatch
                .Elapsed
                .TotalMilliseconds;

        Console.WriteLine();
        Console.WriteLine(
            "系统时间设置成功。");

        /*
         * SetSystemTime() 返回之后，
         * 理论目标时间也应该继续向前流逝。
         *
         * 注意：
         * Stopwatch 从调用 SetSystemTime() 前开始，
         * 因此这里用它估计校时调用本身造成的时间流逝。
         */

        var correctedTime =
            DateTimeOffset.UtcNow;

        /*
         * 为了避免把“读取 UtcNow 之前经过的时间”
         * 计入误差，使用当前验证耗时作为补偿。
         *
         * 此处 Stopwatch 已经停止，因此不能再获得
         * 后续代码耗时。
         *
         * 由于验证本身非常短，实际误差主要来自
         * Windows 系统时间设置粒度及调度。
         */

        var theoreticalCorrectedTime =
            theoreticalTargetTime.AddMilliseconds(
                elapsedAfterSetMilliseconds);

        var verificationError =
            Math.Abs(
                (
                    correctedTime -
                    theoreticalCorrectedTime
                ).TotalMilliseconds);

        Console.WriteLine(
            $"校时后 UTC      : {correctedTime:yyyy-MM-dd HH:mm:ss.fff}");

        Console.WriteLine(
            $"理论目标 UTC    : {theoreticalCorrectedTime:yyyy-MM-dd HH:mm:ss.fff}");

        Console.WriteLine(
            $"验证耗时        : {elapsedAfterSetMilliseconds:F1} ms");

        Console.WriteLine(
            $"剩余误差        : {verificationError:F1} ms");

        if (verificationError <=
            VerificationThresholdMilliseconds)
        {
            Console.WriteLine();
            Console.WriteLine(
                "BTTimeSync v0.7.1 校时完成，" +
                "剩余误差在验证阈值以内。");

            return new SyncCycleResult
            {
                Success = true
            };
        }

        Console.WriteLine();
        Console.WriteLine(
            "BTTimeSync v0.7.1 校时完成，" +
            "但剩余误差超过验证阈值。");

        return new SyncCycleResult
        {
            Success = false
        };
    }

    private static async Task<SyncResult>
        SyncOnceAsync(
            ConnectionContext connection)
    {
        var t1 =
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();

        var payload =
            new byte[8];

        System.Buffers.Binary.BinaryPrimitives
            .WriteInt64BigEndian(
                payload,
                t1);

        var packet =
            new Packet
            {
                Version = 1,
                Type = PacketType.RequestTime,
                Payload = payload
            };

        var encoded =
            PacketWriter.Encode(packet);

        connection.Writer.WriteBytes(encoded);

        await connection.Writer.StoreAsync();
        await connection.Writer.FlushAsync();

        var response =
            await ReceivePacketAsync(connection);

        var t4 =
            DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();

        if (response.Version != 1)
        {
            throw new IOException(
                $"TimeResponse 协议版本错误：{response.Version}");
        }

        if (response.Type != PacketType.TimeResponse)
        {
            throw new IOException(
                $"TimeResponse 类型错误：{response.Type}");
        }

        if (response.Payload.Length != 24)
        {
            throw new IOException(
                $"TimeResponse Payload 长度错误：{response.Payload.Length}");
        }

        var responseT1 =
            System.Buffers.Binary.BinaryPrimitives
                .ReadInt64BigEndian(
                    response.Payload.AsSpan(
                        0,
                        8));

        var t2 =
            System.Buffers.Binary.BinaryPrimitives
                .ReadInt64BigEndian(
                    response.Payload.AsSpan(
                        8,
                        8));

        var t3 =
            System.Buffers.Binary.BinaryPrimitives
                .ReadInt64BigEndian(
                    response.Payload.AsSpan(
                        16,
                        8));

        if (responseT1 != t1)
        {
            throw new IOException(
                "TimeResponse 中的 T1 与请求不一致。");
        }

        /*
         * NTP-style 四时间戳算法：
         *
         * Delay =
         *     (T4 - T1) - (T3 - T2)
         *
         * Offset =
         *     ((T2 - T1) + (T3 - T4)) / 2
         *
         * Offset > 0：
         *     远程设备时间领先本机。
         */

        var delay =
            (t4 - t1) -
            (t3 - t2);

        var offset =
            (
                (t2 - t1) +
                (t3 - t4)
            ) / 2.0;

        var remoteTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(t3);

        var targetTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(t4)
                .AddMilliseconds(offset);

        return new SyncResult
        {
            Success = true,
            RoundTripMilliseconds = delay,
            OffsetMilliseconds = offset,
            RemoteUnixMilliseconds = t3,
            RemoteTime = remoteTime,
            TargetTime = targetTime,
            T1 = t1,
            T2 = t2,
            T3 = t3,
            T4 = t4
        };
    }

    private static async Task<Packet>
        ReceivePacketAsync(
            ConnectionContext connection)
    {
        const uint HeaderLength = 6;

        var header =
            await LoadExactlyAsync(
                connection.Reader,
                HeaderLength);

        var startOfFrame =
            System.Buffers.Binary.BinaryPrimitives
                .ReadUInt16BigEndian(
                    header.AsSpan(
                        0,
                        2));

        if (startOfFrame != Packet.StartOfFrame)
        {
            throw new IOException(
                $"无效的 BTSP SOF：0x{startOfFrame:X4}");
        }

        var payloadLength =
            System.Buffers.Binary.BinaryPrimitives
                .ReadUInt16BigEndian(
                    header.AsSpan(
                        4,
                        2));

        /*
         * Payload 后面还有 2 字节 CRC16。
         */

        var remainingLength =
            (uint)payloadLength + 2u;

        var remaining =
            await LoadExactlyAsync(
                connection.Reader,
                remainingLength);

        var packetData =
            new byte[
                checked(
                    (int)HeaderLength +
                    (int)remainingLength)];

        System.Buffer.BlockCopy(
            header,
            0,
            packetData,
            0,
            (int)HeaderLength);

        System.Buffer.BlockCopy(
            remaining,
            0,
            packetData,
            (int)HeaderLength,
            (int)remainingLength);

        return PacketReader.Decode(packetData);
    }

    private static async Task<byte[]>
        LoadExactlyAsync(
            DataReader reader,
            uint length)
    {
        if (length == 0)
            return [];

        var result =
            new byte[
                checked((int)length)];

        var offset = 0;

        while (offset < result.Length)
        {
            if (_shutdownRequested)
                throw new OperationCanceledException();

            var available =
                reader.UnconsumedBufferLength;

            if (available == 0)
            {
                var remaining =
                    result.Length - offset;

                var requestLength =
                    (uint)Math.Min(
                        remaining,
                        uint.MaxValue);

                var loaded =
                    await reader.LoadAsync(
                        requestLength);

                if (loaded == 0)
                {
                    throw new IOException(
                        "蓝牙连接已关闭。");
                }

                available =
                    reader.UnconsumedBufferLength;
            }

            var toRead =
                (int)Math.Min(
                    available,
                    (uint)(result.Length - offset));

            /*
             * DataReader.ReadBytes() 必须把数据读取到
             * 一个实际的 byte[] 中。
             *
             * 读取后再复制到 result。
             */

            var buffer =
                new byte[toRead];

            reader.ReadBytes(buffer);

            System.Buffer.BlockCopy(
                buffer,
                0,
                result,
                offset,
                toRead);

            offset += toRead;
        }

        return result;
    }

    private static void SetSystemTimeFromUnixMilliseconds(
        long unixMilliseconds)
    {
        var dateTime =
            DateTimeOffset
                .FromUnixTimeMilliseconds(
                    unixMilliseconds)
                .UtcDateTime;

        var systemTime =
            new SYSTEMTIME
            {
                wYear =
                    (ushort)dateTime.Year,

                wMonth =
                    (ushort)dateTime.Month,

                wDay =
                    (ushort)dateTime.Day,

                wHour =
                    (ushort)dateTime.Hour,

                wMinute =
                    (ushort)dateTime.Minute,

                wSecond =
                    (ushort)dateTime.Second,

                wMilliseconds =
                    (ushort)dateTime.Millisecond
            };

        if (!SetSystemTime(
                ref systemTime))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "设置 Windows 系统时间失败。");
        }
    }

    private static bool IsConnectionUsable(
        ConnectionContext? connection)
    {
        return
            connection is not null &&
            connection.Socket is not null &&
            connection.Writer is not null &&
            connection.Reader is not null;
    }

    private static bool IsConnectionException(
        Exception ex)
    {
        if (ex is IOException ||
            ex is SocketException ||
            ex is ObjectDisposedException ||
            ex is System.Runtime.InteropServices.COMException)
        {
            return true;
        }

        var message =
            ex.Message.ToLowerInvariant();

        return message.Contains("connection") ||
               message.Contains("socket") ||
               message.Contains("bluetooth") ||
               message.Contains("closed") ||
               message.Contains("disconnect") ||
               message.Contains("aborted") ||
               message.Contains("远程") ||
               message.Contains("连接") ||
               message.Contains("蓝牙") ||
               message.Contains("中止");
    }
    private static async Task WaitForNextSyncAsync(
        TimeSpan interval)
    {
        var remaining = interval;

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine(
            $"下一次自动校时将在 {SyncIntervalMinutes} 分钟后进行。");
        Console.WriteLine("按 Ctrl+C 可退出程序。");
        Console.WriteLine("========================================");

        while (remaining > TimeSpan.Zero &&
               !_shutdownRequested)
        {
            var display =
                remaining.TotalSeconds >= 60
                    ? $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}"
                    : $"00:{remaining.Seconds:D2}";

            Console.Write($"\r距离下一次校时：{display}   ");

            var delay =
                remaining > TimeSpan.FromSeconds(1)
                    ? TimeSpan.FromSeconds(1)
                    : remaining;

            await DelayWithShutdownAsync(delay);

            remaining -= delay;
        }

        if (!_shutdownRequested)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("到达校时周期，开始下一次自动校时...");
        }
    }
    private static async Task DelayWithShutdownAsync(
        TimeSpan delay)
    {
        const int StepMilliseconds = 200;

        var remaining =
            delay;

        while (remaining > TimeSpan.Zero &&
               !_shutdownRequested)
        {
            var currentDelay =
                remaining >
                TimeSpan.FromMilliseconds(
                    StepMilliseconds)
                    ? TimeSpan.FromMilliseconds(
                        StepMilliseconds)
                    : remaining;

            await Task.Delay(currentDelay);

            remaining -= currentDelay;
        }
    }

    private static void DisposeConnection(
        ConnectionContext? connection)
    {
        if (connection is null)
            return;

        try
        {
            connection.Reader.Dispose();
        }
        catch
        {
        }

        try
        {
            connection.Writer.DetachStream();
        }
        catch
        {
        }

        try
        {
            connection.Writer.Dispose();
        }
        catch
        {
        }

        try
        {
            connection.Socket.Dispose();
        }
        catch
        {
        }

        try
        {
            connection.Service.Dispose();
        }
        catch
        {
        }

        try
        {
            connection.Device.Dispose();
        }
        catch
        {
        }
    }

    private sealed class ConnectionContext
    {
        public BluetoothDevice Device { get; init; } = null!;

        public RfcommDeviceService Service { get; init; } = null!;

        public StreamSocket Socket { get; init; } = null!;

        public DataReader Reader { get; init; } = null!;

        public DataWriter Writer { get; init; } = null!;
    }

    private sealed class SyncCycleResult
    {
        public bool Success { get; init; }

        public bool ConnectionLost { get; init; }
    }
}