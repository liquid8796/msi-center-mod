using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Liệt kê ổ đĩa vật lý (model, dung lượng, nhiệt độ, sức khỏe) cho SSD Status.</summary>
public interface IStorageInfoService
{
    Task<IReadOnlyList<PhysicalDiskInfo>> ReadDisksAsync(CancellationToken ct = default);
}
