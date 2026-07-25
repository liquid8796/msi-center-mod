using System.IO;
using System.Text.Json;
using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;
using MsiCenterMod.Services.System;

namespace MsiCenterMod.Services.Storage;

/// <summary>Cấu hình app tại %ProgramData%\MSI Center Mod\settings.json (ghi atomic).</summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly object _sync = new();

    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "MSI Center Mod",
        "settings.json");

    public AppSettings Current { get; }

    public JsonSettingsService()
    {
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), SerializerOptions)
                       ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Đọc settings.json thất bại — dùng mặc định", ex);
        }

        return new AppSettings();
    }

    public void Save()
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(Current, SerializerOptions));
                File.Move(tempPath, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ghi settings.json thất bại", ex);
            }
        }
    }
}
