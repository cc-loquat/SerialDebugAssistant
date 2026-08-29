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
        var oldThemes = dictionaries.Where(IsThemeDictionary).ToList();
        var insertIndex = oldThemes.Count > 0 ? dictionaries.IndexOf(oldThemes[0]) : 0;
        foreach (var oldTheme in oldThemes) dictionaries.Remove(oldTheme);
        dictionaries.Insert(Math.Clamp(insertIndex, 0, dictionaries.Count), themeDictionary);

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
        return uri.Contains("Colors.xaml", StringComparison.OrdinalIgnoreCase);
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
