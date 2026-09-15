using System.Text.Json;
using BTTimeSync.Common.Models;

namespace BTTimeSync.Notification;

/// <summary>
/// BTTimeSync 校时结果通知数据读取器。
/// </summary>
public sealed class SyncNotificationReader
{
    private const string FilePath =
        @"C:\ProgramData\BTTimeSync\SyncResult.json";

    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>
    /// 读取最新的校时通知数据。
    /// </summary>
    /// <returns>通知数据；文件不存在或读取失败时返回 null。</returns>
    public SyncNotificationData? Read()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var json =
                File.ReadAllText(FilePath);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<SyncNotificationData>(
                json,
                JsonOptions);
        }
        catch (Exception ex)
        {
            global::System.Console.WriteLine(
                $"读取校时结果失败：{ex.Message}");

            return null;
        }
    }
}
