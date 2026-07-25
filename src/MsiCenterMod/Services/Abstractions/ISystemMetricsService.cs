using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Đọc chỉ số hệ thống (CPU/GPU/RAM/đĩa/mạng) cho tab Hardware Monitoring.</summary>
public interface ISystemMetricsService
{
    /// <summary>Tên CPU đầy đủ (đọc một lần từ registry).</summary>
    string CpuName { get; }

    /// <param name="includeGpu">false = bỏ qua nvidia-smi (tránh đánh thức dGPU khi không cần).</param>
    Task<SystemMetrics> ReadAsync(bool includeGpu = true, CancellationToken ct = default);
}
