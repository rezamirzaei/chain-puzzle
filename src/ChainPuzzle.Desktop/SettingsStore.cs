using System.Text.Json;

namespace ChainPuzzle.Desktop;

/// <summary>Serialisable document for user settings.</summary>
internal sealed record SettingsDocument(
    int Version,
    bool SoundEnabled,
    int AnimationSpeed,
    bool ShowHintHighlights,
    bool ExpertMode)
{
    public static SettingsDocument Default { get; } =
        new(SettingsStore.CurrentVersion, false, 1, true, false);
}

/// <summary>Reads and writes user settings to a local JSON file.</summary>
internal sealed class SettingsStore : ISettingsStore
{
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public SettingsStore(string? rootDirectory = null)
    {
        _filePath = StoragePaths.Resolve("settings.json", rootDirectory);
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
            return document is not null && document.Version is 1 or CurrentVersion
                ? Normalize(document)
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
            JsonFileStore.SaveAtomic(_filePath, Normalize(document), JsonOptions);
        }
        catch
        {
            // Saving settings should never break the game loop.
        }
    }

    private static SettingsDocument Normalize(SettingsDocument? document)
    {
        if (document is null)
        {
            return SettingsDocument.Default;
        }

        return new SettingsDocument(
            CurrentVersion,
            document.SoundEnabled,
            Math.Clamp(document.AnimationSpeed, 0, 2),
            document.ShowHintHighlights,
            document.ExpertMode);
    }
}
