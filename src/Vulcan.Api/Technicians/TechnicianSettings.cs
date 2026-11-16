using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Vulcan.Api.Technicians;

/// <summary>
/// Settings that belong to this computer, in %LOCALAPPDATA%\VulcanCBAS\settings.json: the last technician
/// name (M1-42) and the cloud folder (SCRUM-214). Not in the database: they belong to the computer, not the data.
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
            Write(Read() with { LastTechnician = name });
        }
    }

    /// <summary>The folder "Save to cloud" writes to (SCRUM-214), or null if none is set.</summary>
    public string? CloudFolder
    {
        get
        {
            lock (_lock)
            {
                return Read().CloudFolder;
            }
        }
    }

    /// <summary>Saves the cloud folder, or clears it when <paramref name="path"/> is null.</summary>
    public void SaveCloudFolder(string? path)
    {
        lock (_lock)
        {
            Write(Read() with { CloudFolder = path });
        }
    }

    private void Write(SettingsFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(file, _json));
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

        public string? CloudFolder { get; init; }
    }
}
