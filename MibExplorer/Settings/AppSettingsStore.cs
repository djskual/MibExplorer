using System.IO;
using System.Text.Json;

namespace MibExplorer.Settings;

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MibExplorer");

    private static readonly string SettingsPath =
        Path.Combine(SettingsFolder, "settings.json");

    private static AppSettings _current = LoadInternal();

    public static AppSettings Current => _current;

    public static void Save(AppSettings settings)
    {
        var copy = settings.Clone();
        copy.Normalize();

        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(copy, JsonOptions));

        _current = copy;
    }

    private static AppSettings LoadInternal()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
}