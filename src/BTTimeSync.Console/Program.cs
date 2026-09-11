using BTTimeSync.Bluetooth.Interfaces;
using BTTimeSync.Bluetooth.Services;
using BTTimeSync.Console.Application;
using BTTimeSync.Console.Configuration;
using BTTimeSync.Console.Infrastructure;
using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Services;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text;

internal class Program
{
    private static readonly IConfiguration Configuration =
        BuildConfiguration();

    private static readonly AppConfig AppConfig =
        Configuration
            .Get<AppConfig>()
        ?? throw new InvalidOperationException(
            "无法加载 BTTimeSync 内嵌配置。");

    private static readonly CancellationTokenSource ShutdownCts = new();

    private static volatile bool _shutdownRequested;

    private static IConfiguration BuildConfiguration()
    {
        var assembly =
            Assembly.GetExecutingAssembly();

        const string resourceName =
            "BTTimeSync.Console.appsettings.json";

        using Stream? stream =
            assembly.GetManifestResourceStream(
                resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"找不到内嵌配置资源：{resourceName}");
        }

        return new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
    }

    private static async Task Main()
    {
        Console.OutputEncoding =
            Encoding.UTF8;

        Console.WriteLine(
            "BTTimeSync v0.8.0");

        Console.WriteLine(
            "===============");

        Console.WriteLine();

        Console.CancelKeyPress +=
            OnCancelKeyPress;

        IBluetoothService? bluetoothService = null;

        try
        {
            bluetoothService =
                new BluetoothService();

            IPacketTransport packetTransport =
                new PacketTransport(
                    bluetoothService);

            IBtspSession btspSession =
                new BtspSession(
                    packetTransport);

            ITimeSyncService timeSyncService =
                new TimeSyncService(
                    packetTransport);

            ITimeSyncSampler timeSyncSampler =
                new TimeSyncSampler(
                    timeSyncService);

            ISystemClock systemClock =
                new WindowsSystemClock();

            var application =
                new TimeSyncApplication(
                    bluetoothService,
                    btspSession,
                    timeSyncService,
                    timeSyncSampler,
                    systemClock,
                    AppConfig,
                    ShutdownCts.Token);

            await application.RunAsync();
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
            try
            {
                if (bluetoothService is not null)
                {
                    await bluetoothService
                        .DisconnectAsync();
                }
            }
            catch
            {
            }

            ShutdownCts.Dispose();

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

        try
        {
            ShutdownCts.Cancel();
        }
        catch
        {
        }

        Console.WriteLine();
        Console.WriteLine(
            "正在退出 BTTimeSync...");
    }
}