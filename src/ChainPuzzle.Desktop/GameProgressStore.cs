using System.Text.Json;

namespace ChainPuzzle.Desktop;

/// <summary>Serialisable document for persisting game progress between sessions.</summary>
internal sealed record GameProgressDocument(
    int Version,
    int CurrentLevelIndex,
    string[] CompletedLevelIds,
    Dictionary<string, int> BestMovesByLevelId,
    string? CurrentStatePattern,
    int CurrentMoves)
{
    /// <summary>An empty document with default values.</summary>
    public static GameProgressDocument Empty { get; } =
        new(GameProgressStore.CurrentVersion, 0, Array.Empty<string>(), new Dictionary<string, int>(), null, 0);
}

/// <summary>Reads and writes game progress (chapter index, board state, clears, best moves) to a local JSON file.</summary>
internal sealed class GameProgressStore : IGameProgressStore
{
    private sealed record LegacyGameProgressDocumentV3(
        int Version,
        int CurrentLevelIndex,
        string[] CompletedLevelIds,
        Dictionary<string, int> BestMovesByLevelId);

    public const int CurrentVersion = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public GameProgressStore(string? rootDirectory = null)
    {
        _filePath = StoragePaths.Resolve("progress.json", rootDirectory);
    }

    public GameProgressDocument Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return GameProgressDocument.Empty;
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return GameProgressDocument.Empty;
            }

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(GameProgressDocument.Version), out var versionElement)
                || versionElement.ValueKind != JsonValueKind.Number)
            {
                return GameProgressDocument.Empty;
            }

            return versionElement.GetInt32() switch
            {
                CurrentVersion => Normalize(JsonSerializer.Deserialize<GameProgressDocument>(json, JsonOptions)),
                3 => MigrateFromV3(JsonSerializer.Deserialize<LegacyGameProgressDocumentV3>(json, JsonOptions)),
                _ => GameProgressDocument.Empty
            };
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
            JsonFileStore.SaveAtomic(_filePath, Normalize(document), JsonOptions);
        }
        catch
        {
            // Saving progress should never break the game loop.
        }
    }

    private static GameProgressDocument MigrateFromV3(LegacyGameProgressDocumentV3? legacy)
    {
        if (legacy is null)
        {
            return GameProgressDocument.Empty;
        }

        return new GameProgressDocument(
            CurrentVersion,
            Math.Max(0, legacy.CurrentLevelIndex),
            legacy.CompletedLevelIds ?? Array.Empty<string>(),
            legacy.BestMovesByLevelId is null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(legacy.BestMovesByLevelId, StringComparer.Ordinal),
            null,
            0);
    }

    private static GameProgressDocument Normalize(GameProgressDocument? document)
    {
        if (document is null)
        {
            return GameProgressDocument.Empty;
        }

        return new GameProgressDocument(
            CurrentVersion,
            Math.Max(0, document.CurrentLevelIndex),
            document.CompletedLevelIds ?? Array.Empty<string>(),
            document.BestMovesByLevelId is null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(document.BestMovesByLevelId, StringComparer.Ordinal),
            string.IsNullOrWhiteSpace(document.CurrentStatePattern) ? null : document.CurrentStatePattern,
            Math.Max(0, document.CurrentMoves));
    }
}
