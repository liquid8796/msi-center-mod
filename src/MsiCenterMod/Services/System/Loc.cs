using System.Globalization;
using System.Windows;

namespace MsiCenterMod.Services.System;

/// <summary>
/// Quản lý ngôn ngữ UI: hoán đổi ResourceDictionary Strings.{lang}.xaml lúc chạy.
/// XAML dùng {DynamicResource S.*} nên tự cập nhật ngay; chuỗi sinh từ ViewModel
/// dùng Get/Format và làm mới qua sự kiện <see cref="LanguageChanged"/>.
/// </summary>
public static class Loc
{
    public const string Vietnamese = "vi";
    public const string English = "en";

    public static string CurrentLanguage { get; private set; } = Vietnamese;

    /// <summary>Phát sau khi đổi ngôn ngữ — ViewModel refresh các chuỗi đã render.</summary>
    public static event Action? LanguageChanged;

    /// <summary>Đặt ngôn ngữ lúc khởi động (không phát sự kiện).</summary>
    public static void Initialize(string? language) => Apply(language, raiseEvent: false);

    /// <summary>Đổi ngôn ngữ lúc chạy.</summary>
    public static void SetLanguage(string? language)
    {
        if (Normalize(language) == CurrentLanguage)
        {
            return;
        }

        Apply(language, raiseEvent: true);
    }

    public static string Get(string key)
        => global::System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args);
        }
        catch (FormatException)
        {
            return Get(key);
        }
    }

    private static string Normalize(string? language)
        => string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Vietnamese;

    private static void Apply(string? language, bool raiseEvent)
    {
        string normalized = Normalize(language);
        var app = global::System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        try
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri($"Themes/Strings.{normalized}.xaml", UriKind.Relative),
            };

            var merged = app.Resources.MergedDictionaries;
            ResourceDictionary? existing = merged.FirstOrDefault(
                d => d.Source?.OriginalString.Contains("/Strings.", StringComparison.OrdinalIgnoreCase) == true
                     || d.Source?.OriginalString.StartsWith("Themes/Strings.", StringComparison.OrdinalIgnoreCase) == true);

            if (existing is not null)
            {
                merged[merged.IndexOf(existing)] = dictionary;
            }
            else
            {
                merged.Add(dictionary);
            }

            CurrentLanguage = normalized;
            if (raiseEvent)
            {
                LanguageChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Đổi ngôn ngữ sang '{normalized}' thất bại", ex);
        }
    }
}
