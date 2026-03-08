using ChainPuzzle.Core;
using ChainPuzzle.Desktop;
using ChainPuzzle.Desktop.ViewModels;
using Xunit;

namespace ChainPuzzle.Tests;

public sealed class DesktopStateTests
{
    [Fact]
    public void GameViewModel_Constructor_DoesNotSaveSettingsWhileLoading()
    {
        var progressStore = new InMemoryProgressStore();
        var settingsStore = new InMemorySettingsStore(new SettingsDocument(
            SettingsStore.CurrentVersion,
            SoundEnabled: true,
            AnimationSpeed: 2,
            ShowHintHighlights: false));

        var viewModel = CreateViewModel(progressStore, settingsStore);

        Assert.Equal(0, settingsStore.SaveCalls);
        Assert.True(viewModel.SoundEnabled);
        Assert.Equal(2, viewModel.AnimationSpeed);
        Assert.False(viewModel.ShowHintHighlights);
    }

    [Fact]
    public void GameViewModel_TryRotate_PersistsAndRestoresCurrentBoard()
    {
        var progressStore = new InMemoryProgressStore();
        var settingsStore = new InMemorySettingsStore();
        var viewModel = CreateViewModel(progressStore, settingsStore);
        var move = viewModel.Game.GetHintMove();

        Assert.True(move.HasValue);
        Assert.True(viewModel.TryRotate(move.Value.JointIndex, move.Value.Rotation));
        Assert.NotNull(progressStore.Document.CurrentStatePattern);
        Assert.Equal(viewModel.CurrentState.SerializeSegments(), progressStore.Document.CurrentStatePattern);
        Assert.Equal(viewModel.Moves, progressStore.Document.CurrentMoves);

        var restoredViewModel = CreateViewModel(progressStore, new InMemorySettingsStore());

        Assert.Equal(viewModel.LevelIndex, restoredViewModel.LevelIndex);
        Assert.Equal(viewModel.Moves, restoredViewModel.Moves);
        Assert.Equal(viewModel.CurrentState.SerializeSegments(), restoredViewModel.CurrentState.SerializeSegments());
        Assert.True(restoredViewModel.CanUndo);
        restoredViewModel.UndoCommand.Execute(null);
        Assert.Equal(0, restoredViewModel.Moves);
        Assert.True(restoredViewModel.CanRedo);
    }

    [Fact]
    public async Task GameViewModel_FindHintCommand_StaysTextOnlyWhenHighlightsAreDisabled()
    {
        var viewModel = CreateViewModel(new InMemoryProgressStore(), new InMemorySettingsStore());
        viewModel.ShowHintHighlights = false;

        await viewModel.FindHintCommand.ExecuteAsync(null);

        Assert.Null(viewModel.SelectedJointIndex);
        Assert.StartsWith("Nudge:", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void GameViewModel_ChapterCardsExposeProgressAndBranchMetrics()
    {
        var viewModel = CreateViewModel(new InMemoryProgressStore(), new InMemorySettingsStore());

        Assert.Equal(viewModel.Levels.Count, viewModel.ChapterCards.Count);

        var first = viewModel.ChapterCards[0];
        var final = viewModel.ChapterCards[^1];

        Assert.True(first.IsCurrent);
        Assert.Equal("Open", first.MedalLabel);
        Assert.Equal("01", first.NumberText);
        Assert.Contains("Par 6", first.PressureText, StringComparison.Ordinal);
        Assert.Contains("traps 25", first.PressureText, StringComparison.Ordinal);
        Assert.Contains("Decoys 14", first.BranchText, StringComparison.Ordinal);

        Assert.Equal("10", final.NumberText);
        Assert.Contains("Par 8", final.PressureText, StringComparison.Ordinal);
        Assert.Contains("shell-4 4009", final.BranchText, StringComparison.Ordinal);
    }

    [Fact]
    public void GameViewModel_OpenChapterFromHome_JumpsAndClosesOverlay()
    {
        var viewModel = CreateViewModel(new InMemoryProgressStore(), new InMemorySettingsStore());

        Assert.True(viewModel.IsHomeVisible);

        viewModel.OpenChapterFromHome(7);

        Assert.Equal(7, viewModel.LevelIndex);
        Assert.False(viewModel.IsHomeVisible);
        Assert.Equal("Jumped to Shield.", viewModel.StatusMessage);
    }

    [Fact]
    public void GameProgressStore_Load_MigratesVersion4Document()
    {
        var rootDirectory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(rootDirectory, "progress.json"), """
                {
                  "Version": 4,
                  "CurrentLevelIndex": 2,
                  "CompletedLevelIds": ["c1", "c2"],
                  "BestMovesByLevelId": {
                    "c1": 6
                  },
                  "CurrentStatePattern": "E2,A,B3,C,D2,",
                  "CurrentMoves": 4
                }
                """);

            var store = new GameProgressStore(rootDirectory);
            var document = store.Load();

            Assert.Equal(GameProgressStore.CurrentVersion, document.Version);
            Assert.Equal(2, document.CurrentLevelIndex);
            Assert.Equal(4, document.CurrentMoves);
            Assert.Equal("E2,A,B3,C,D2,", document.CurrentStatePattern);
            Assert.Empty(document.UndoHistory);
            Assert.Empty(document.RedoHistory);
            Assert.Equal(["c1", "c2"], document.CompletedLevelIds);
            Assert.Equal(6, document.BestMovesByLevelId["c1"]);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void GameProgressStore_Load_MigratesVersion3Document()
    {
        var rootDirectory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(rootDirectory, "progress.json"), """
                {
                  "Version": 3,
                  "CurrentLevelIndex": 2,
                  "CompletedLevelIds": ["c1", "c2"],
                  "BestMovesByLevelId": {
                    "c1": 6
                  }
                }
                """);

            var store = new GameProgressStore(rootDirectory);
            var document = store.Load();

            Assert.Equal(GameProgressStore.CurrentVersion, document.Version);
            Assert.Equal(2, document.CurrentLevelIndex);
            Assert.Equal(0, document.CurrentMoves);
            Assert.Null(document.CurrentStatePattern);
            Assert.Empty(document.UndoHistory);
            Assert.Empty(document.RedoHistory);
            Assert.Equal(["c1", "c2"], document.CompletedLevelIds);
            Assert.Equal(6, document.BestMovesByLevelId["c1"]);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void SettingsStore_Save_ClampsAnimationSpeedBeforePersisting()
    {
        var rootDirectory = CreateTempDirectory();

        try
        {
            var store = new SettingsStore(rootDirectory);
            store.Save(new SettingsDocument(SettingsStore.CurrentVersion, true, 99, false));

            var document = store.Load();

            Assert.True(document.SoundEnabled);
            Assert.Equal(2, document.AnimationSpeed);
            Assert.False(document.ShowHintHighlights);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static GameViewModel CreateViewModel(IGameProgressStore progressStore, ISettingsStore settingsStore)
    {
        return new GameViewModel(
            new ChapterGame(ChapterFactory.CreateChapters()),
            progressStore,
            settingsStore);
    }

    private static string CreateTempDirectory()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "ChainPuzzle.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        return rootDirectory;
    }

    private sealed class InMemoryProgressStore : IGameProgressStore
    {
        public GameProgressDocument Document { get; private set; } = GameProgressDocument.Empty;

        public int SaveCalls { get; private set; }

        public GameProgressDocument Load() => Document;

        public void Save(GameProgressDocument document)
        {
            SaveCalls += 1;
            Document = document;
        }
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private SettingsDocument _document;

        public InMemorySettingsStore(SettingsDocument? document = null)
        {
            _document = document ?? SettingsDocument.Default;
        }

        public int SaveCalls { get; private set; }

        public SettingsDocument Load() => _document;

        public void Save(SettingsDocument document)
        {
            SaveCalls += 1;
            _document = document;
        }
    }
}
