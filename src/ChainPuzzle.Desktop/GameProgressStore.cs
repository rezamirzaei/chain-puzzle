using System.Text.Json;

namespace ChainPuzzle.Desktop;

/// <summary>Serialisable document for persisting game progress between sessions.</summary>
internal sealed record GameProgressDocument(
    int Version,
    int CurrentLevelIndex,
    string[] CompletedLevelIds,
    Dictionary<string, int> BestMovesByLevelId)
{
    /// <summary>An empty document with default values.</summary>
    public static GameProgressDocument Empty { get; } =
        new(GameProgressStore.CurrentVersion, 0, Array.Empty<string>(), new Dictionary<string, int>());
}

/// <summary>Reads and writes game progress (current chapter, completed chapters, best moves) to a local JSON file.</summary>
internal sealed class GameProgressStore
{
    public const int CurrentVersion = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public GameProgressStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChainPuzzle");
        _filePath = Path.Combine(root, "progress.json");
    }

    public GameProgressDocument Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return GameProgressDocument.Empty;
            }

            using var stream = File.OpenRead(_filePath);
            var document = JsonSerializer.Deserialize<GameProgressDocument>(stream, JsonOptions);
            return document is not null && document.Version == CurrentVersion
                ? document
                : GameProgressDocument.Empty;
        }
        catch
        {
            return GameProgressDocument.Empty;
        }
    }

    public void Save(GameProgressDocument document)
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
            // Saving progress should never break the game loop.
        }
    }
}
