using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Khởi động cùng Windows qua Task Scheduler (schtasks /XML):
///  - RunLevel HighestAvailable → không hiện UAC mỗi lần logon.
///  - Tắt điều kiện pin (mặc định schtasks không chạy task khi dùng pin).
///  - Chạy với tham số --autostart → app mở ẩn dưới khay hệ thống.
/// </summary>
public sealed class StartupTaskService : IStartupService
{
    private const string TaskName = "MSI Center Mod";

    public bool IsEnabled()
        => RunSchtasks($"/Query /TN \"{TaskName}\"", out _) == 0;

    public bool TrySetEnabled(bool enabled, out string error)
    {
        try
        {
            if (!enabled)
            {
                int deleteCode = RunSchtasks($"/Delete /TN \"{TaskName}\" /F", out string deleteOutput);
                error = deleteCode == 0 ? string.Empty : deleteOutput;
                return deleteCode == 0;
            }

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                error = "Không xác định được đường dẫn exe.";
                return false;
            }

            string xmlPath = Path.Combine(Path.GetTempPath(), "MsiCenterMod_task.xml");
            // schtasks yêu cầu file XML đúng encoding khai báo (UTF-16).
            File.WriteAllText(xmlPath, BuildTaskXml(exePath), Encoding.Unicode);
            try
            {
                int createCode = RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F", out string createOutput);
                error = createCode == 0 ? string.Empty : createOutput;
                return createCode == 0;
            }
            finally
            {
                try
                {
                    File.Delete(xmlPath);
                }
                catch
                {
                    // file tạm — bỏ qua
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("TrySetEnabled autostart thất bại", ex);
            error = ex.Message;
            return false;
        }
    }

    private static string BuildTaskXml(string exePath)
    {
        string user = WindowsIdentity.GetCurrent().Name;
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Khởi động MSI Center Mod cùng Windows (ẩn dưới khay hệ thống).</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>true</StartWhenAvailable>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <AllowHardTerminate>false</AllowHardTerminate>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{exePath}</Command>
                  <Arguments>--autostart</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static int RunSchtasks(string arguments, out string output)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        if (process is null)
        {
            output = "Không chạy được schtasks.exe";
            return -1;
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return process.ExitCode;
    }
}
