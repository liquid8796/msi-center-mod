using System.Diagnostics;
using System.Runtime.InteropServices;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.System;

/// <summary>
/// "Free Up Memory": gọi EmptyWorkingSet trên mọi tiến trình truy cập được —
/// buộc Windows trim các trang RAM không dùng (kỹ thuật chuẩn, an toàn:
/// trang cần thiết sẽ được nạp lại theo yêu cầu).
/// </summary>
public sealed class MemoryCleanerService : IMemoryCleaner
{
    public Task<(int ProcessCount, double FreedMb)> TrimWorkingSetsAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            double beforeMb = ReadAvailableMb();
            int trimmed = 0;

            foreach (Process process in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (EmptyWorkingSet(process.Handle))
                    {
                        trimmed++;
                    }
                }
                catch
                {
                    // tiến trình hệ thống/bảo vệ — bỏ qua
                }
                finally
                {
                    process.Dispose();
                }
            }

            Thread.Sleep(500); // chờ hệ thống cập nhật số liệu
            double freed = Math.Max(0, ReadAvailableMb() - beforeMb);
            AppLogger.Info($"Trim working set: {trimmed} tiến trình, giải phóng ~{freed:0} MB");
            return (trimmed, freed);
        }, ct);

    private static double ReadAvailableMb()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? status.ullAvailPhys / 1024.0 / 1024 : 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(nint hProcess);
}
