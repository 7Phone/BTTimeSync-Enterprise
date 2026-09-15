using System.Text.Json;
using BTTimeSync.Common.Models;

namespace BTTimeSync.Console.Application;

/// <summary>
/// BTTimeSync 校时结果通知数据写入器。
/// </summary>
public sealed class SyncNotificationWriter
{
    private const string DirectoryPath =
        @"C:\ProgramData\BTTimeSync";

    private const string FilePath =
        @"C:\ProgramData\BTTimeSync\SyncResult.json";

    private const string TemporaryFilePath =
        @"C:\ProgramData\BTTimeSync\SyncResult.json.tmp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 写入一次校时结果。
    /// </summary>
    /// <param name="result">校时结果。</param>
    public void Write(SyncNotificationData result)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);

            var json =
                JsonSerializer.Serialize(
                    result,
                    JsonOptions);

            File.WriteAllText(
                TemporaryFilePath,
                json);

            File.Move(
                TemporaryFilePath,
                FilePath,
                overwrite: true);
        }
        catch (Exception ex)
        {
            global::System.Console.WriteLine(
                $"通知结果写入失败：{ex.Message}");
        }
    }
}
