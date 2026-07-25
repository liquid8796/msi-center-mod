namespace MsiCenterMod.Services.Abstractions;

/// <summary>Giải phóng RAM bằng cách trim working set các tiến trình truy cập được.</summary>
public interface IMemoryCleaner
{
    /// <summary>Trả về (số tiến trình đã trim, số MB giải phóng được).</summary>
    Task<(int ProcessCount, double FreedMb)> TrimWorkingSetsAsync(CancellationToken ct = default);
}
