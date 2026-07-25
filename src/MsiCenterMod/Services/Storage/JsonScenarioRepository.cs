using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Storage;

/// <summary>
/// Lưu scenario dưới dạng JSON tại %ProgramData%\MSI Center Mod\scenarios.json.
/// Ghi kiểu atomic (ghi file tạm rồi đổi tên) để không mất dữ liệu nếu app bị tắt giữa chừng.
/// </summary>
public sealed class JsonScenarioRepository : IScenarioRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string StorePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "MSI Center Mod",
        "scenarios.json");

    public IReadOnlyList<ScenarioProfile> LoadAll()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                var loaded = JsonSerializer.Deserialize<List<ScenarioProfile>>(
                    File.ReadAllText(StorePath), SerializerOptions);
                if (loaded is { Count: > 0 })
                {
                    foreach (ScenarioProfile profile in loaded)
                    {
                        Normalize(profile);
                    }

                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // File hỏng → giữ file cũ làm .bak để người dùng cứu dữ liệu, dùng bộ mặc định.
            TryBackupCorruptFile();
        }

        return CreateSeedScenarios();
    }

    public void SaveAll(IEnumerable<ScenarioProfile> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        string directory = Path.GetDirectoryName(StorePath)!;
        Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(scenarios.ToList(), SerializerOptions);
        string tempPath = StorePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, StorePath, overwrite: true);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                File.Copy(StorePath, StorePath + ".bak", overwrite: true);
            }
        }
        catch
        {
            // Sao lưu chỉ là nỗ lực tốt nhất — không chặn app khởi động.
        }
    }

    private static void Normalize(ScenarioProfile profile)
    {
        if (profile.CpuFanCurve is not { Length: FanCurve.PointCount })
        {
            profile.CpuFanCurve = (int[])ScenarioProfile.DefaultCpuFanCurve.Clone();
        }

        if (profile.GpuFanCurve is not { Length: FanCurve.PointCount })
        {
            profile.GpuFanCurve = (int[])ScenarioProfile.DefaultGpuFanCurve.Clone();
        }

        profile.CpuFanCurve = profile.CpuFanCurve.Select(FanCurve.Clamp).ToArray();
        profile.GpuFanCurve = profile.GpuFanCurve.Select(FanCurve.Clamp).ToArray();

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = "Scenario chưa đặt tên";
        }
    }

    /// <summary>Bộ scenario mẫu cho lần chạy đầu — người dùng sửa/xóa thoải mái.</summary>
    private static List<ScenarioProfile> CreateSeedScenarios() =>
    [
        new ScenarioProfile
        {
            Name = "Gaming tối đa",
            Glyph = "🎮",
            Performance = PerformanceLevel.Turbo,
            FanMode = FanMode.Advanced,
            CpuFanCurve = [0, 45, 55, 65, 75, 85],
            GpuFanCurve = [0, 50, 60, 70, 80, 90],
            PowerOverlay = PowerOverlayMode.BestPerformance,
        },
        new ScenarioProfile
        {
            Name = "Làm việc yên tĩnh",
            Glyph = "💻",
            Performance = PerformanceLevel.Balanced,
            FanMode = FanMode.Silent,
            PowerOverlay = PowerOverlayMode.Balanced,
        },
        new ScenarioProfile
        {
            Name = "Pin tối đa",
            Glyph = "🔋",
            Performance = PerformanceLevel.Eco,
            FanMode = FanMode.Silent,
            PowerOverlay = PowerOverlayMode.BestEfficiency,
        },
    ];
}
