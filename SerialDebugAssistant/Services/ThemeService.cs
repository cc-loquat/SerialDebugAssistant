using System;
using System.Collections;
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
    private const string LumiTheme = "Lumin";
    private const string LegacyLumiTheme = "Lumi";
    private const string MellowTheme = "Mellow";
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
                if (settings?.Theme is DarkTheme or LightTheme or LumiTheme or LegacyLumiTheme or MellowTheme or SystemTheme)
                    return settings.Theme == LegacyLumiTheme ? LumiTheme : settings.Theme;
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
        var sourceName = selection is LumiTheme or LegacyLumiTheme
            ? "Themes/Colors.Lumi.xaml"
            : selection == MellowTheme ? "Themes/Colors.Mellow.xaml"
            : (selection == LightTheme || (selection == SystemTheme && UsesLightSystemTheme())
                ? "Themes/Colors.Light.xaml" : "Themes/Colors.xaml");
        var source = new Uri(sourceName, UriKind.Relative);
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        var themeDictionary = new ResourceDictionary { Source = source };
        var oldThemes = dictionaries.Where(IsThemeDictionary).ToList();
        foreach (var oldTheme in oldThemes) dictionaries.Remove(oldTheme);
        dictionaries.Add(themeDictionary);

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
               uri.EndsWith("Themes/Colors.Mellow.xaml", StringComparison.OrdinalIgnoreCase) ||
               uri is "Colors.xaml" or "Colors.Light.xaml" or "Colors.Lumi.xaml" or "Colors.Mellow.xaml";
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
