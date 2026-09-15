using BTTimeSync.Common.Models;

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace BTTimeSync.Notification;

internal static class Program
{
    private const string LastShownFile =
        @"C:\ProgramData\BTTimeSync\LastShown.txt";

    private static int Main()
    {
        var reader =
            new SyncNotificationReader();

        var result =
            reader.Read();

        if (result is null)
        {
            return 0;
        }

        if (AlreadyShown(result.SyncTime))
        {
            return 0;
        }

        var manager =
            AppNotificationManager.Default;

        manager.NotificationInvoked +=
            (_, args) =>
            {
                global::System.Console.WriteLine(
                    $"通知被点击：{args.Argument}");
            };

        manager.Register();

        try
        {
            var notification =
                BuildNotification(result);

            manager.Show(notification);

            SaveLastShown(result.SyncTime);

            return 0;
        }
        finally
        {
            manager.Unregister();
        }
    }

    private static bool AlreadyShown(
        DateTimeOffset syncTime)
    {
        try
        {
            if (!File.Exists(LastShownFile))
            {
                return false;
            }

            var text =
                File.ReadAllText(LastShownFile);

            return text == syncTime.ToString("O");
        }
        catch
        {
            return false;
        }
    }

    private static void SaveLastShown(
        DateTimeOffset syncTime)
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(LastShownFile)!);

            File.WriteAllText(
                LastShownFile,
                syncTime.ToString("O"));
        }
        catch
        {
            // 不影响通知
        }
    }

    private static AppNotification BuildNotification(
        SyncNotificationData result)
    {
        var detail =
            $"偏移 {result.FinalOffsetMilliseconds:+0.00;-0.00;0.00} ms · " +
            $"延迟 {result.BestDelayMilliseconds:F1} ms · " +
            $"剩余误差 {result.RemainingErrorMilliseconds:F1} ms · " +
            $"样本 {result.SampleCount}";

        var builder =
            new AppNotificationBuilder()
                .AddArgument("action", "open")
                .AddText("BTTimeSync")
                .AddText(GetTitle(result))
                .AddText(detail);

        return builder.BuildNotification();
    }

    private static string GetTitle(
        SyncNotificationData result)
    {
        if (result.ConnectionLost)
        {
            return "❌ 蓝牙连接断开";
        }

        if (result.Success)
        {
            return "✓ 校时成功";
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return "⚠ 校时完成（需关注）";
        }

        return "⚠ 校时失败";
    }
}
