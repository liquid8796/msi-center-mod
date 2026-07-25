using System.IO;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Logger tối giản ghi ra %ProgramData%\MSI Center Mod\logs\app.log.
/// Mọi lỗi ghi log đều bị nuốt — log không bao giờ được phép làm app chết.
/// </summary>
public static class AppLogger
{
    private static readonly object Sync = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "MSI Center Mod", "logs", "app.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} :: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

                // Giữ log gọn: quá 2 MB thì xoay vòng sang .old
                var file = new FileInfo(LogPath);
                if (file.Exists && file.Length > 2 * 1024 * 1024)
                {
                    File.Copy(LogPath, LogPath + ".old", overwrite: true);
                    File.Delete(LogPath);
                }

                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // bỏ qua — không để logging phá app
        }
    }
}
