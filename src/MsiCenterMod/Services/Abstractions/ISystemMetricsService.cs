using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Đọc chỉ số hệ thống (CPU/GPU/RAM/đĩa/mạng) cho tab Hardware Monitoring.</summary>
public interface ISystemMetricsService
{
    /// <summary>Tên CPU đầy đủ (đọc một lần từ registry).</summary>
    string CpuName { get; }

    Task<SystemMetrics> ReadAsync(CancellationToken ct = default);
}
