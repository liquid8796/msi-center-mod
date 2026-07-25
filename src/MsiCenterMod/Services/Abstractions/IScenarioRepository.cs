using MsiCenterMod.Models;

namespace MsiCenterMod.Services.Abstractions;

/// <summary>Kho lưu trữ scenario (hiện tại là file JSON trong ProgramData).</summary>
public interface IScenarioRepository
{
    /// <summary>Đường dẫn file lưu trữ — hiển thị cho người dùng biết dữ liệu nằm đâu.</summary>
    string StorePath { get; }

    IReadOnlyList<ScenarioProfile> LoadAll();

    void SaveAll(IEnumerable<ScenarioProfile> scenarios);
}
