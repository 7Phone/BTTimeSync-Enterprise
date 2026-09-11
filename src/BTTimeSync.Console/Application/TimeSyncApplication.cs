using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Common.Models;
using BTTimeSync.Console.Infrastructure;
using BTTimeSync.Core;
using BTTimeSync.Core.Interfaces;
using System.Diagnostics;
using System.Net.Sockets;

namespace BTTimeSync.Console.Application;

/// <summary>
/// BTTimeSync 应用程序主流程。
/// </summary>
/// <remarks>
/// 负责协调蓝牙连接、BTSP 握手、时间同步、
/// 系统时间设置、校时验证以及自动重连。
/// </remarks>
public sealed class TimeSyncApplication
{
    private const int SampleCount = 10;

    private const int SampleIntervalMilliseconds = 100;

    private const int SyncIntervalMinutes = 30;

    private const double VerificationThresholdMilliseconds = 50.0;

    private const int ReconnectRetryIntervalSeconds = 5;

    private const int ReconnectRetryIntervalMaximumSeconds = 30;

    private readonly IBluetoothService _bluetoothService;

    private readonly IBtspSession _btspSession;

    private readonly ITimeSyncService _timeSyncService;

    private readonly ITimeSyncSampler _timeSyncSampler;

    private readonly ISystemClock _systemClock;

    private readonly CancellationToken _cancellationToken;

    public TimeSyncApplication(
        IBluetoothService bluetoothService,
        IBtspSession btspSession,
        ITimeSyncService timeSyncService,
        ITimeSyncSampler timeSyncSampler,
        ISystemClock systemClock,
        CancellationToken cancellationToken)
    {
        _bluetoothService =
            bluetoothService ??
            throw new ArgumentNullException(
                nameof(bluetoothService));

        _btspSession =
            btspSession ??
            throw new ArgumentNullException(
                nameof(btspSession));

        _timeSyncService =
            timeSyncService ??
            throw new ArgumentNullException(
                nameof(timeSyncService));

        _timeSyncSampler =
            timeSyncSampler ??
            throw new ArgumentNullException(
                nameof(timeSyncSampler));

        _systemClock =
            systemClock ??
            throw new ArgumentNullException(
                nameof(systemClock));

        _cancellationToken =
            cancellationToken;
    }

    /// <summary>
    /// 运行 BTTimeSync 主循环。
    /// </summary>
    public async Task RunAsync()
    {
        await ConnectAndHandshakeAsync();

        while (!_cancellationToken.IsCancellationRequested)
        {
            if (!_bluetoothService.IsConnected)
            {
                await _bluetoothService
                    .DisconnectAsync();

                await ReconnectAsync();

                if (_cancellationToken.IsCancellationRequested)
                    break;
            }

            var syncResult =
                await RunSyncCycleAsync();

            if (_cancellationToken.IsCancellationRequested)
                break;

            if (syncResult.ConnectionLost)
            {
                global::System.Console.WriteLine();
                global::System.Console.WriteLine(
                    "检测到蓝牙连接断开。");

                global::System.Console.WriteLine(
                    "准备自动重新连接...");

                await _bluetoothService
                    .DisconnectAsync();

                await ReconnectAsync();

                if (_cancellationToken.IsCancellationRequested)
                    break;

                continue;
            }

            await WaitForNextSyncAsync(
                TimeSpan.FromMinutes(
                    SyncIntervalMinutes));
        }
    }

    /// <summary>
    /// 初次连接并完成握手。
    /// </summary>
    private async Task ConnectAndHandshakeAsync()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndHandshakeOnceAsync();

                return;
            }
            catch (Exception ex)
                when (IsConnectionException(ex))
            {
                global::System.Console.WriteLine();
                global::System.Console.WriteLine(
                    $"连接失败：{ex.Message}");

                if (_cancellationToken.IsCancellationRequested)
                    break;

                global::System.Console.WriteLine(
                    $"将在 {ReconnectRetryIntervalSeconds} 秒后重试...");

                await DelayWithCancellationAsync(
                    TimeSpan.FromSeconds(
                        ReconnectRetryIntervalSeconds));
            }
        }

        throw new OperationCanceledException(
            _cancellationToken);
    }

    /// <summary>
    /// 执行一次连接和握手。
    /// </summary>
    private async Task ConnectAndHandshakeOnceAsync()
    {
        global::System.Console.WriteLine(
            "正在搜索 BTTimeSync 蓝牙设备...");

        var devices =
            await _bluetoothService
                .DiscoverDevicesAsync(
                    _cancellationToken);

        BluetoothDeviceInfo? targetDevice = null;

        foreach (var device in devices)
        {
            _cancellationToken
                .ThrowIfCancellationRequested();

            var deviceName =
                string.IsNullOrWhiteSpace(device.Name)
                    ? "(未命名设备)"
                    : device.Name;

            global::System.Console.WriteLine(
                $"发现设备：{deviceName}");

            if (!device.IsTimeSyncDevice)
                continue;

            targetDevice = device;

            global::System.Console.WriteLine(
                "发现 BTTimeSync RFCOMM 服务。");

            break;
        }

        if (targetDevice is null)
        {
            throw new IOException(
                "未找到 BTTimeSync RFCOMM 服务。");
        }

        await _bluetoothService.ConnectAsync(
            targetDevice,
            _cancellationToken);

        global::System.Console.WriteLine(
            "RFCOMM 连接成功。");

        await _btspSession.HandshakeAsync(
            _cancellationToken);

        global::System.Console.WriteLine(
            "BTSP Hello/HelloAck 握手成功。");

        global::System.Console.WriteLine();
    }

    /// <summary>
    /// 自动重连。
    /// </summary>
    private async Task ReconnectAsync()
    {
        var retrySeconds =
            ReconnectRetryIntervalSeconds;

        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                global::System.Console.WriteLine();
                global::System.Console.WriteLine(
                    "========== 自动重连 ==========");

                await ConnectAndHandshakeOnceAsync();

                global::System.Console.WriteLine(
                    "自动重连成功。");

                global::System.Console.WriteLine(
                    "==============================");

                return;
            }
            catch (Exception ex)
                when (IsConnectionException(ex))
            {
                global::System.Console.WriteLine();
                global::System.Console.WriteLine(
                    $"自动重连失败（{ex.GetType().Name}）：");

                global::System.Console.WriteLine(
                    $"HResult: 0x{ex.HResult:X8}");

                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    global::System.Console.WriteLine(
                        ex.Message);
                }

                if (_cancellationToken.IsCancellationRequested)
                    break;

                global::System.Console.WriteLine(
                    $"{retrySeconds} 秒后再次尝试...");

                await DelayWithCancellationAsync(
                    TimeSpan.FromSeconds(
                        retrySeconds));

                retrySeconds =
                    Math.Min(
                        retrySeconds * 2,
                        ReconnectRetryIntervalMaximumSeconds);
            }
        }

        throw new OperationCanceledException(
            _cancellationToken);
    }

    /// <summary>
    /// 执行一轮时间同步。
    /// </summary>
    private async Task<SyncCycleResult>
        RunSyncCycleAsync()
    {
        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            "========================================");

        global::System.Console.WriteLine(
            $"开始时间同步：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        global::System.Console.WriteLine(
            "========================================");

        TimeSyncSampleResult sampleResult;

        try
        {
            sampleResult =
                await _timeSyncSampler.CollectAsync(
                    SampleCount,
                    TimeSpan.FromMilliseconds(
                        SampleIntervalMilliseconds),
                    _cancellationToken);
        }
        catch (Exception ex)
            when (IsConnectionException(ex))
        {
            global::System.Console.WriteLine();
            global::System.Console.WriteLine(
                $"时间同步过程中连接断开：{ex.Message}");

            return new SyncCycleResult
            {
                Success = false,
                ConnectionLost = true
            };
        }

        var samples =
            sampleResult.Samples;

        for (var i = 0;
             i < samples.Count;
             i++)
        {
            var result =
                samples[i];

            global::System.Console.WriteLine(
                $"样本 #{i + 1:00}  " +
                $"Delay={result.RoundTripMilliseconds,6:F1} ms  " +
                $"Offset={result.OffsetMilliseconds,8:F1} ms");
        }

        if (samples.Count == 0)
        {
            return new SyncCycleResult
            {
                Success = false
            };
        }

        var statistics =
            sampleResult.Statistics;

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            "---------- 统计结果 ----------");

        global::System.Console.WriteLine(
            $"Median Offset : {statistics.Median:+0.00;-0.00;0.00} ms");

        global::System.Console.WriteLine(
            $"MAD           : {statistics.Mad:F2} ms");

        global::System.Console.WriteLine(
            $"Threshold     : ±{statistics.Threshold:F2} ms");

        global::System.Console.WriteLine(
            $"正常样本       : {statistics.ValidIndexes.Count}");

        global::System.Console.WriteLine(
            $"异常样本       : {statistics.OutlierIndexes.Count}");

        global::System.Console.WriteLine(
            $"Final Offset  : {statistics.FinalOffset:+0.00;-0.00;0.00} ms");

        var bestIndex =
            sampleResult.BestSampleIndex;

        var bestResult =
            sampleResult.BestSample;

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            "---------- 最佳样本 ----------");

        global::System.Console.WriteLine(
            $"样本编号       : #{bestIndex + 1:00}");

        global::System.Console.WriteLine(
            $"最佳 Delay     : {bestResult.RoundTripMilliseconds:F1} ms");

        global::System.Console.WriteLine(
            $"对应 Offset    : {bestResult.OffsetMilliseconds:+0.00;-0.00;0.00} ms");

        var targetUnixMilliseconds =
            sampleResult.TargetUnixMilliseconds;

        var theoreticalTargetTime =
            sampleResult.TargetTime;

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            $"参考 T4       : {bestResult.T4}");

        global::System.Console.WriteLine(
            $"远程 UTC      : {bestResult.RemoteTime:yyyy-MM-dd HH:mm:ss.fff}");

        global::System.Console.WriteLine(
            $"目标 UTC      : {theoreticalTargetTime:yyyy-MM-dd HH:mm:ss.fff}");

        global::System.Console.WriteLine(
            $"最终 Offset    : {statistics.FinalOffset:+0.00;-0.00;0.00} ms");

        var verificationStopwatch =
            Stopwatch.StartNew();

        var systemClockTarget =
            DateTimeOffset
                .FromUnixTimeMilliseconds(
                    targetUnixMilliseconds);

        _systemClock.SetUtcTime(
            systemClockTarget);

        verificationStopwatch.Stop();

        var elapsedAfterSetMilliseconds =
            verificationStopwatch
                .Elapsed
                .TotalMilliseconds;

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
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

        global::System.Console.WriteLine(
            $"校时后 UTC      : {correctedTime:yyyy-MM-dd HH:mm:ss.fff}");

        global::System.Console.WriteLine(
            $"理论目标 UTC    : {theoreticalCorrectedTime:yyyy-MM-dd HH:mm:ss.fff}");

        global::System.Console.WriteLine(
            $"验证耗时        : {elapsedAfterSetMilliseconds:F1} ms");

        global::System.Console.WriteLine(
            $"剩余误差        : {verificationError:F1} ms");

        if (verificationError <=
            VerificationThresholdMilliseconds)
        {
            global::System.Console.WriteLine();
            global::System.Console.WriteLine(
                "BTTimeSync v0.8.0 校时完成，" +
                "剩余误差在验证阈值以内。");

            return new SyncCycleResult
            {
                Success = true
            };
        }

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            "BTTimeSync v0.8.0 校时完成，" +
            "但剩余误差超过验证阈值。");

        return new SyncCycleResult
        {
            Success = false
        };
    }

    /// <summary>
    /// 等待下一次自动校时。
    /// </summary>
    private async Task WaitForNextSyncAsync(
        TimeSpan interval)
    {
        var remaining =
            interval;

        global::System.Console.WriteLine();
        global::System.Console.WriteLine(
            "========================================");

        global::System.Console.WriteLine(
            $"下一次自动校时将在 {SyncIntervalMinutes} 分钟后进行。");

        global::System.Console.WriteLine(
            "按 Ctrl+C 可退出程序。");

        global::System.Console.WriteLine(
            "========================================");

        while (remaining > TimeSpan.Zero &&
               !_cancellationToken.IsCancellationRequested)
        {
            var display =
                remaining.TotalSeconds >= 60
                    ? $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}"
                    : $"00:{remaining.Seconds:D2}";

            global::System.Console.Write(
                $"\r距离下一次校时：{display}   ");

            var delay =
                remaining > TimeSpan.FromSeconds(1)
                    ? TimeSpan.FromSeconds(1)
                    : remaining;

            await DelayWithCancellationAsync(
                delay);

            remaining -= delay;
        }

        if (!_cancellationToken.IsCancellationRequested)
        {
            global::System.Console.WriteLine();
            global::System.Console.WriteLine();

            global::System.Console.WriteLine(
                "到达校时周期，开始下一次自动校时...");
        }
    }

    /// <summary>
    /// 带取消令牌的延迟。
    /// </summary>
    private async Task DelayWithCancellationAsync(
        TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return;

        await Task.Delay(
            delay,
            _cancellationToken);
    }

    /// <summary>
    /// 判断异常是否属于连接类异常。
    /// </summary>
    private static bool IsConnectionException(
        Exception ex)
    {
        if (ex is IOException ||
            ex is SocketException ||
            ex is ObjectDisposedException ||
            ex is global::System.Runtime.InteropServices.COMException)
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

    /// <summary>
    /// 单次校时周期结果。
    /// </summary>
    private sealed class SyncCycleResult
    {
        public bool Success { get; init; }

        public bool ConnectionLost { get; init; }
    }
}