using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SerialDebugAssistant.Services;

public static class ThemeService
{
    private const string DarkTheme = "深色";
    private const string LightTheme = "浅色";
    private const string SystemTheme = "跟随系统";
    private const string LumiTheme = "Lumi";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Comm Terminal", "settings.json");
    private static readonly HashSet<object> AppliedThemeKeys = new();

    public static string LoadTheme()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<ThemeSettings>(File.ReadAllText(SettingsPath));
                if (settings?.Theme is DarkTheme or LightTheme or LumiTheme or SystemTheme)
                    return settings.Theme;
            }
        }
        catch
        {
            // A damaged preference file should not prevent startup.
        }

        return DarkTheme;
    }

    public static void Apply(string selection, bool save = true)
    {
        var sourceName = selection == LumiTheme
            ? "Themes/Colors.Lumi.xaml"
            : (selection == LightTheme || (selection == SystemTheme && UsesLightSystemTheme())
                ? "Themes/Colors.Light.xaml" : "Themes/Colors.xaml");
        var source = new Uri(sourceName, UriKind.Relative);
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var themeDictionary = new ResourceDictionary { Source = source };
        foreach (var key in AppliedThemeKeys.ToList()) Application.Current.Resources.Remove(key);
        AppliedThemeKeys.Clear();
        foreach (var entry in themeDictionary)
        {
            Application.Current.Resources[entry.Key] = entry.Value;
            AppliedThemeKeys.Add(entry.Key);
        }

        // Keep the compiled dictionaries for fallback keys; application-level entries above
        // take precedence and DynamicResource receives a direct change notification.

        if (!save) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new ThemeSettings { Theme = selection }));
        }
        catch
        {
            // Theme persistence is optional.
        }
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var uri = dictionary.Source?.OriginalString ?? string.Empty;
        return uri.EndsWith("Themes/Colors.xaml", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith("Themes/Colors.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith("Themes/Colors.Lumi.xaml", StringComparison.OrdinalIgnoreCase) ||
               uri is "Colors.xaml" or "Colors.Light.xaml" or "Colors.Lumi.xaml";
    }

    private static bool UsesLightSystemTheme()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 0) is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ThemeSettings
    {
        public string Theme { get; set; } = DarkTheme;
    }
}
