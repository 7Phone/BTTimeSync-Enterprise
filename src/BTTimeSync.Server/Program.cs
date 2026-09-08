using BTTimeSync.Bluetooth.Services;
using BTTimeSync.Common;

namespace BTTimeSync.Server;

internal class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("BTTimeSync RFCOMM Server");
        System.Console.WriteLine("========================");
        System.Console.WriteLine();

        System.Console.WriteLine(
            $"服务名称：{AppConstants.BluetoothServiceName}");

        System.Console.WriteLine(
            $"服务 UUID：{AppConstants.BluetoothServiceUuid}");

        System.Console.WriteLine();

        var server = new BluetoothRfcommServer();

        try
        {
            System.Console.WriteLine(
                "正在启动 RFCOMM Server...");

            await server.StartAsync();

            System.Console.WriteLine();

            System.Console.WriteLine(
                "RFCOMM Server 启动成功！");

            System.Console.WriteLine();

            System.Console.WriteLine(
                "正在广播 BTTimeSync RFCOMM 服务：");

            System.Console.WriteLine(
                AppConstants.BluetoothServiceUuid);

            System.Console.WriteLine();

            System.Console.WriteLine(
                "等待内网机客户端连接...");

            System.Console.WriteLine();

            System.Console.WriteLine(
                "按任意键停止 Server 并退出...");

            System.Console.ReadKey();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine();

            System.Console.WriteLine(
                "RFCOMM Server 启动失败！");

            System.Console.WriteLine();

            System.Console.WriteLine(
                $"异常类型：{ex.GetType().FullName}");

            System.Console.WriteLine(
                $"异常信息：{ex.Message}");

            if (ex.InnerException is not null)
            {
                System.Console.WriteLine();

                System.Console.WriteLine(
                    $"内部异常：{ex.InnerException.Message}");
            }

            System.Console.WriteLine();

            System.Console.WriteLine(
                "按任意键退出...");

            System.Console.ReadKey();

            return;
        }

        server.Dispose();
    }
}