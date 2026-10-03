using System.Diagnostics; // Debug
using System.IO;          // File, Directory, Path; WPF projects don't import this automatically
using System.Text.Json;   // JsonSerializer, JsonSerializerOptions, JsonException

namespace SysMonitor.App.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as a JSON file in the user's AppData folder.
/// Never throws: if the file is missing or broken, you get the defaults; if saving fails,
/// it's logged and the app carries on.
/// </summary>
internal static class SettingsStore
{
    /// <summary>
    /// Full path of the settings file, e.g. C:\Users\you\AppData\Roaming\Datchik\settings.json.
    /// Path.Combine joins folder and file names with the right separators.
    /// </summary>
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Datchik",
        "settings.json");

    /// <summary>JSON formatting: one setting per line, so the file is easy to read and edit by hand.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Reads the settings file. Returns the defaults if it doesn't exist yet or can't be read.
    /// </summary>
    public static AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);

            // Deserialize = turn JSON text back into an object. It returns null if the file
            // contains just "null"; "?? new AppSettings()" falls back to the defaults then.
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        // "when (...)" = only catch these specific kinds of errors: the file is locked or
        // unreadable (IOException, UnauthorizedAccessException), or it isn't valid JSON
        // (JsonException). Anything else is a real bug and should not be hidden.
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Could not load settings, using defaults: {exception.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Writes the settings file, creating the Datchik folder if needed.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public static void Save(AppSettings settings)
    {
        try
        {
            // Does nothing if the folder already exists.
            // The "!" tells the compiler "this isn't null": our path always has a folder part.
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

            // Serialize = turn the object into JSON text.
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not save settings: {exception.Message}");
        }
    }
}
