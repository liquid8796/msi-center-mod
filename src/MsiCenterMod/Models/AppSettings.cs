namespace MsiCenterMod.Models;

/// <summary>Cấu hình app (lưu ProgramData\MSI Center Mod\settings.json).</summary>
public sealed class AppSettings
{
    /// <summary>Tự áp lại scenario cuối cùng khi khởi động / resume / đổi nguồn AC-pin.</summary>
    public bool AutoReapplyEnabled { get; set; } = true;

    public Guid? LastAppliedScenarioId { get; set; }
}
