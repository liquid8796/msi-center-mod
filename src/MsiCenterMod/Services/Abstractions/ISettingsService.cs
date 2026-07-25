using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Đọc/ghi cấu hình app (settings.json).</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    void Save();
}
