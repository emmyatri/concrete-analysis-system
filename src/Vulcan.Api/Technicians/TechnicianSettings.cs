using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Vulcan.Api.Technicians;

/// <summary>
/// Remembers the last technician name on this computer (M1-42), in
/// %LOCALAPPDATA%\VulcanCBAS\settings.json. Not in the database: it belongs to the computer, not the data.
/// </summary>
public sealed class TechnicianSettings
{
    /// <summary>Config key that moves the folder. Tests point it at a temp folder.</summary>
    public const string DirectoryKey = "Settings:Directory";

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _lock = new();

    public TechnicianSettings(IConfiguration config)
    {
        var directory = config[DirectoryKey];
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VulcanCBAS");
        }

        _path = Path.Combine(directory, "settings.json");
    }

    /// <summary>The last name used on this computer, or null if there isn't one yet.</summary>
    public string? LastTechnician
    {
        get
        {
            lock (_lock)
            {
                return Read().LastTechnician;
            }
        }
    }

    public void SaveLastTechnician(string name)
    {
        lock (_lock)
        {
            var file = Read() with { LastTechnician = name };
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(file, _json));
        }
    }

    /// <summary>A missing or unreadable file counts as empty, so the app still starts.</summary>
    private SettingsFile Read()
    {
        if (!File.Exists(_path))
        {
            return new SettingsFile();
        }

        try
        {
            return JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_path)) ?? new SettingsFile();
        }
        catch (JsonException)
        {
            return new SettingsFile();
        }
    }

    private sealed record SettingsFile
    {
        public string? LastTechnician { get; init; }
    }
}
