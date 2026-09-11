using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BTTimeSync.Console.Infrastructure;

/// <summary>
/// Windows 系统时钟实现。
/// </summary>
public sealed class WindowsSystemClock : ISystemClock
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

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern bool SetSystemTime(
        ref SYSTEMTIME st);

    /// <inheritdoc />
    public void SetUtcTime(
        DateTimeOffset utcTime)
    {
        var dateTime =
            utcTime.UtcDateTime;

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
}