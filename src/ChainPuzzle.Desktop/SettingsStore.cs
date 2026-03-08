using System.Text.Json;

namespace ChainPuzzle.Desktop;

/// <summary>Serialisable document for user settings.</summary>
internal sealed record SettingsDocument(
    int Version,
    bool SoundEnabled,
    int AnimationSpeed,
    bool ShowHintHighlights)
{
    public static SettingsDocument Default { get; } =
        new(SettingsStore.CurrentVersion, false, 1, true);
}

/// <summary>Reads and writes user settings to a local JSON file.</summary>
internal sealed class SettingsStore
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public SettingsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChainPuzzle");
        _filePath = Path.Combine(root, "settings.json");
    }

    public SettingsDocument Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return SettingsDocument.Default;
            }

            using var stream = File.OpenRead(_filePath);
            var document = JsonSerializer.Deserialize<SettingsDocument>(stream, JsonOptions);
            return document is not null && document.Version == CurrentVersion
                ? document
                : SettingsDocument.Default;
        }
        catch
        {
            return SettingsDocument.Default;
        }
    }

    public void Save(SettingsDocument document)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Create(_filePath);
            JsonSerializer.Serialize(stream, document, JsonOptions);
        }
        catch
        {
            // Saving settings should never break the game loop.
        }
    }
}

