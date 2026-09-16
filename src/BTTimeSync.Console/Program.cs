using BTTimeSync.Application;
using BTTimeSync.Bluetooth;
using BTTimeSync.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

internal class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("BTTimeSync Console");
        Console.WriteLine("=================");
        Console.WriteLine();

        var configuration =
            new ConfigurationBuilder()
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: false)
                .Build();


        var services =
            new ServiceCollection();


        services
            .AddCore()
            .AddBluetooth()
            .AddApplication(configuration);


        using var serviceProvider =
            services.BuildServiceProvider();


        var application =
            serviceProvider
                .GetRequiredService<BTTimeSync.Application.TimeSyncApplication>();


        using var cts =
            new CancellationTokenSource();


        Console.CancelKeyPress +=
            (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();

                Console.WriteLine();
                Console.WriteLine(
                    "正在退出 BTTimeSync...");
            };


        try
        {
            await application.RunAsync(
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(
                "BTTimeSync 已停止。");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "运行异常：");

            Console.WriteLine(ex);
        }
    }
}