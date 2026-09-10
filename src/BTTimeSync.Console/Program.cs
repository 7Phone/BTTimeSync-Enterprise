using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Services;
using BTTimeSync.Common;
using BTTimeSync.Core;
using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

internal class Program
{
    private const int SampleCount = 10;
    private const int SampleIntervalMilliseconds = 100;
    private const int SyncIntervalMinutes = 30;
    private const double VerificationThresholdMilliseconds = 50.0;
    private const int ReconnectRetryIntervalSeconds = 5;
    private const int ReconnectRetryIntervalMaximumSeconds = 30;

    private static volatile bool _shutdownRequested;
    private static IBluetoothService? _bluetoothService;

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

        try
        {
            _bluetoothService =
                new BluetoothService();

            ITimeSyncService timeSyncService =
                new TimeSyncService(_bluetoothService);
            await ConnectAndHandshakeAsync();

            while (!_shutdownRequested)
            {
                if (!_bluetoothService.IsConnected)
                {
                    await _bluetoothService.DisconnectAsync();

                    await ReconnectAsync();

                    if (_shutdownRequested)
                        break;
                }

                var syncResult =
                    await RunSyncCycleAsync(
                        timeSyncService);

                if (_shutdownRequested)
                    break;

                if (syncResult.ConnectionLost)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "检测到蓝牙连接断开。");

                    Console.WriteLine(
                        "准备自动重新连接...");

                    await _bluetoothService
                        .DisconnectAsync();

                    await ReconnectAsync();

                    if (_shutdownRequested)
                        break;

                    continue;
                }

                await WaitForNextSyncAsync(
                    TimeSpan.FromMinutes(
                        SyncIntervalMinutes));
            }
        }
        catch (OperationCanceledException)
            when (_shutdownRequested)
        {
            Console.WriteLine();
            Console.WriteLine(
                "收到退出请求。");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(
                "程序发生未处理异常：");

            Console.WriteLine(ex);
        }
        finally
        {
            if (_bluetoothService is not null)
            {
                try
                {
                    await _bluetoothService
                        .DisconnectAsync();
                }
                catch
                {
                }

                _bluetoothService = null;
            }

            Console.WriteLine();
            Console.WriteLine(
                "BTTimeSync 已退出。");
        }
    }

    private static void OnCancelKeyPress(
        object? sender,
        ConsoleCancelEventArgs e)
    {
        e.Cancel = true;

        _shutdownRequested = true;

        Console.WriteLine();
        Console.WriteLine(
            "正在退出 BTTimeSync...");
    }

    private static async Task ConnectAndHandshakeAsync()
    {
        if (_bluetoothService is null)
        {
            throw new InvalidOperationException(
                "BluetoothService 尚未初始化。");
        }

        while (!_shutdownRequested)
        {
            try
            {
                await ConnectAndHandshakeOnceAsync();

                return;
            }
            catch (Exception ex)
                when (IsConnectionException(ex))
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"连接失败：{ex.Message}");

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

    private static async Task ConnectAndHandshakeOnceAsync()
    {
        if (_bluetoothService is null)
        {
            throw new InvalidOperationException(
                "BluetoothService 尚未初始化。");
        }

        Console.WriteLine(
            "正在搜索 BTTimeSync 蓝牙设备...");

        var devices =
            await _bluetoothService
                .DiscoverDevicesAsync();

        BTTimeSync.Common.Models.BluetoothDeviceInfo? targetDevice = null;

        foreach (var device in devices)
        {
            if (_shutdownRequested)
                throw new OperationCanceledException();

            var deviceName =
                string.IsNullOrWhiteSpace(device.Name)
                    ? "(未命名设备)"
                    : device.Name;

            Console.WriteLine(
                $"发现设备：{deviceName}");

            if (!device.IsTimeSyncDevice)
                continue;

            targetDevice = device;

            Console.WriteLine(
                "发现 BTTimeSync RFCOMM 服务。");

            break;
        }

        if (targetDevice is null)
        {
            throw new IOException(
                "未找到 BTTimeSync RFCOMM 服务。");
        }

        await _bluetoothService.ConnectAsync(
            targetDevice);

        Console.WriteLine(
            "RFCOMM 连接成功。");

        Console.WriteLine(
            "BTSP Hello/HelloAck 握手成功。");

        Console.WriteLine();
    }

    private static async Task ReconnectAsync()
    {
        if (_bluetoothService is null)
        {
            throw new InvalidOperationException(
                "BluetoothService 尚未初始化。");
        }

        var retrySeconds =
            ReconnectRetryIntervalSeconds;

        while (!_shutdownRequested)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine(
                    "========== 自动重连 ==========");

                await ConnectAndHandshakeOnceAsync();

                Console.WriteLine(
                    "自动重连成功。");

                Console.WriteLine(
                    "==============================");

                return;
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
                    Console.WriteLine(
                        ex.Message);
                }

                if (_shutdownRequested)
                    break;

                Console.WriteLine(
                    $"{retrySeconds} 秒后再次尝试...");

                await DelayWithShutdownAsync(
                    TimeSpan.FromSeconds(
                        retrySeconds));

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
            ITimeSyncService timeSyncService)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"开始时间同步：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine(
            "========================================");

        var results =
            new List<BTTimeSync.Common.SyncResult>();

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
                    await timeSyncService
                        .SyncOnceAsync();

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

        var correctedTime =
            DateTimeOffset.UtcNow;

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
        var remaining =
            interval;

        Console.WriteLine();
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"下一次自动校时将在 {SyncIntervalMinutes} 分钟后进行。");

        Console.WriteLine(
            "按 Ctrl+C 可退出程序。");

        Console.WriteLine(
            "========================================");

        while (remaining > TimeSpan.Zero &&
               !_shutdownRequested)
        {
            var display =
                remaining.TotalSeconds >= 60
                    ? $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}"
                    : $"00:{remaining.Seconds:D2}";

            Console.Write(
                $"\r距离下一次校时：{display}   ");

            var delay =
                remaining > TimeSpan.FromSeconds(1)
                    ? TimeSpan.FromSeconds(1)
                    : remaining;

            await DelayWithShutdownAsync(
                delay);

            remaining -= delay;
        }

        if (!_shutdownRequested)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(
                "到达校时周期，开始下一次自动校时...");
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

            await Task.Delay(
                currentDelay);

            remaining -= currentDelay;
        }
    }

    private sealed class SyncCycleResult
    {
        public bool Success { get; init; }

        public bool ConnectionLost { get; init; }
    }
}