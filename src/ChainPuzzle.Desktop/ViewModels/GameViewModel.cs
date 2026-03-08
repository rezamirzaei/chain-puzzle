using System.Globalization;
using System.Threading.Tasks;
using ChainPuzzle.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChainPuzzle.Desktop.ViewModels;

/// <summary>
/// Primary view model for the Chain Chapters game, managing all game state,
/// chapter navigation, progress persistence, and player actions.
/// </summary>
public sealed partial class GameViewModel : ObservableObject
{
    private readonly ChapterGame _game;
    private readonly IGameProgressStore _progressStore;
    private readonly ISettingsStore _settingsStore;
    private readonly Dictionary<string, int> _bestMovesByLevelId = new(StringComparer.Ordinal);
    private bool _isLoadingSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(CanRotateManually))]
    private bool _isFindingHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(CanRotateManually))]
    private bool _isAnimating;

    [ObservableProperty]
    private bool _isHomeVisible;

    [ObservableProperty]
    private bool _homeAllowsClose;

    [ObservableProperty]
    private int? _selectedJointIndex;

    [ObservableProperty]
    private string _statusMessage = "Drag a joint to rotate the chain and cover the target shape.";

    // --- Settings ---

    [ObservableProperty]
    private bool _soundEnabled;

    [ObservableProperty]
    private int _animationSpeed = 1; // 0=slow, 1=normal, 2=fast

    [ObservableProperty]
    private bool _showHintHighlights = true;

    public GameViewModel() : this(
        new ChapterGame(ChapterFactory.CreateChapters()),
        new GameProgressStore(),
        new SettingsStore())
    {
    }

    internal GameViewModel(ChapterGame game, IGameProgressStore progressStore, ISettingsStore settingsStore)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _progressStore = progressStore ?? throw new ArgumentNullException(nameof(progressStore));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        LoadProgress();
        LoadSettings();
        ShowHome(allowClose: false);
        RefreshAll();
    }

    // --- Derived properties ---

    public bool IsBusy => IsAnimating || IsFindingHint;
    public bool CanInteract => !IsBusy && !IsHomeVisible;
    public bool CanRotateManually => CanInteract && !IsSolved && SelectedJointIndex is not null;

    public ChapterGame Game => _game;
    public IReadOnlyList<ChainLevel> Levels => _game.Levels;
    public ChainLevel CurrentLevel => _game.CurrentLevel;
    public ChainState CurrentState => _game.CurrentState;
    public int LevelIndex => _game.LevelIndex;
    public int Moves => _game.Moves;
    public bool IsSolved => _game.IsSolved;
    public bool CanUndo => _game.CanUndo;
    public bool CanRedo => _game.CanRedo;

    // --- HUD text properties ---

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _completedText = "";
    [ObservableProperty] private string _movesText = "";
    [ObservableProperty] private string _bestText = "";
    [ObservableProperty] private string _badgeText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _accentHex = "#0F766E";

    // --- Solved card ---

    [ObservableProperty] private bool _showSolvedCard;
    [ObservableProperty] private string _solvedTitle = "";
    [ObservableProperty] private string _solvedSummary = "";
    [ObservableProperty] private bool _canAdvance;

    // --- Home overlay ---

    [ObservableProperty] private string _homeTitle = "";
    [ObservableProperty] private string _homeSummary = "";
    [ObservableProperty] private string _homeProgressInfo = "";
    [ObservableProperty] private string _homeBestInfo = "";
    [ObservableProperty] private string _homeMedalInfo = "";
    [ObservableProperty] private string _homePrimaryLabel = "";
    [ObservableProperty] private bool _homeSecondaryVisible;

    // --- Chapter picker ---

    [ObservableProperty] private string[] _chapterPickerItems = Array.Empty<string>();
    [ObservableProperty] private int _chapterPickerSelectedIndex;

    // --- Badge style ---

    [ObservableProperty] private string _badgeBg = "#1E1D4ED8";
    [ObservableProperty] private string _badgeBorder = "#421D4ED8";
    [ObservableProperty] private string _badgeFg = "#1D4ED8";

    private bool _hasSavedProgress;

    private bool HasSavedRunData => LevelIndex != 0
        || Moves > 0
        || _game.CompletedLevelIds.Count > 0
        || _bestMovesByLevelId.Count > 0;

    /// <summary>Animation duration in milliseconds, based on the current speed setting.</summary>
    public int AnimationDurationMs => AnimationSpeed switch
    {
        0 => 400,
        2 => 100,
        _ => 220
    };

    // ====================
    //  Actions / Commands
    // ====================

    /// <summary>Attempt a rotation. Returns true if the move was applied.</summary>
    public bool TryRotate(int jointIndex, int rotation)
    {
        if (IsBusy || IsSolved) return false;
        if (!_game.TryRotate(jointIndex, rotation, out _))
        {
            StatusMessage = "Move blocked by chain collision.";
            RefreshAll();
            return false;
        }

        StatusMessage = $"Moved joint {jointIndex}.";
        SaveProgress();
        RefreshAll();
        return true;
    }

    /// <summary>Called by the view when the rotation animation finishes.</summary>
    public void OnAnimationCompleted()
    {
        IsAnimating = false;
        if (IsSolved) FinalizeSolvedState();
        RefreshAll();
    }

    [RelayCommand]
    private void GoHome()
    {
        if (IsBusy) return;
        ShowHome(allowClose: true);
        RefreshAll();
    }

    [RelayCommand]
    private void HomePrimary()
    {
        StatusMessage = _hasSavedProgress
            ? "Progress restored."
            : "Pick a joint and start covering the silhouette.";
        HideHome();
    }

    [RelayCommand]
    private void HomeSecondary() => StartNewRun();

    [RelayCommand]
    private void HomeClose() => HideHome();

    [RelayCommand]
    private void PreviousLevel() => ChangeLevel(LevelIndex - 1);

    [RelayCommand]
    private void NextLevel() => ChangeLevel(LevelIndex + 1);

    [RelayCommand]
    private void Undo()
    {
        if (IsBusy || !_game.TryUndo()) return;
        StatusMessage = "Undid the last move.";
        SaveProgress();
        RefreshAll();
    }

    [RelayCommand]
    private void Redo()
    {
        if (IsBusy || !_game.TryRedo()) return;
        if (IsSolved) FinalizeSolvedState();
        else StatusMessage = "Replayed the move.";
        SaveProgress();
        RefreshAll();
    }

    [RelayCommand]
    private void ResetLevel()
    {
        _game.ResetLevel();
        SelectedJointIndex = null;
        StatusMessage = "Chapter reset.";
        SaveProgress();
        RefreshAll();
    }

    [RelayCommand]
    private async Task FindHint()
    {
        if (IsBusy || IsSolved) return;

        IsFindingHint = true;
        StatusMessage = "Computing nudge...";
        RefreshAll();

        ChainMove? hint;
        try
        {
            hint = await Task.Run(() => _game.GetHintMove());
        }
        finally
        {
            IsFindingHint = false;
        }

        if (hint is null)
        {
            StatusMessage = "No nudge available right now.";
        }
        else
        {
            if (ShowHintHighlights)
            {
                SelectedJointIndex = hint.Value.JointIndex;
            }
            StatusMessage = $"Nudge: try joint {hint.Value.JointIndex}, rotate {FormatRotation(hint.Value.Rotation)}.";
        }

        RefreshAll();
    }

    [RelayCommand]
    private void RotateLeft()
    {
        if (SelectedJointIndex is null) { StatusMessage = "Select a joint first."; RefreshAll(); return; }
        TryRotate(SelectedJointIndex.Value, -1);
    }

    [RelayCommand]
    private void RotateRight()
    {
        if (SelectedJointIndex is null) { StatusMessage = "Select a joint first."; RefreshAll(); return; }
        TryRotate(SelectedJointIndex.Value, 1);
    }

    [RelayCommand]
    private void SolvedNext()
    {
        if (LevelIndex < Levels.Count - 1) ChangeLevel(LevelIndex + 1);
    }

    [RelayCommand]
    private void SolvedReset() => ResetLevel();

    public void SelectChapter(int index)
    {
        if (index < 0 || index == LevelIndex) return;
        ChangeLevel(index);
    }

    public void SelectJoint(int? jointIndex)
    {
        SelectedJointIndex = jointIndex;
        if (jointIndex.HasValue) StatusMessage = $"Joint {jointIndex.Value} selected.";
        RefreshAll();
    }

    public void SaveSettings()
    {
        _settingsStore.Save(new SettingsDocument(
            SettingsStore.CurrentVersion,
            SoundEnabled,
            AnimationSpeed,
            ShowHintHighlights));
    }

    // ====================
    //  Internal helpers
    // ====================

    private void ChangeLevel(int nextIndex, string? status = null)
    {
        _game.SetLevel(nextIndex);
        SelectedJointIndex = null;
        StatusMessage = status ?? $"{CurrentLevel.Subtitle} loaded.";
        SaveProgress();
        RefreshAll();
    }

    private void ShowHome(bool allowClose)
    {
        HomeAllowsClose = allowClose;
        IsHomeVisible = true;
        RefreshAll();
    }

    private void HideHome()
    {
        IsHomeVisible = false;
        HomeAllowsClose = false;
        RefreshAll();
    }

    private void StartNewRun()
    {
        _bestMovesByLevelId.Clear();
        _game.CompletedLevelIds.Clear();
        _game.SetLevel(0);
        SelectedJointIndex = null;
        StatusMessage = "New run started.";
        SaveProgress();
        HideHome();
    }

    private void FinalizeSolvedState()
    {
        SelectedJointIndex = null;
        var isNewBest = RecordBestRun();
        StatusMessage = isNewBest
            ? $"{CurrentLevel.Subtitle} solved in {Moves} moves. New personal best."
            : $"{CurrentLevel.Subtitle} solved in {Moves} moves.";
        SaveProgress();
    }

    private bool RecordBestRun()
    {
        var id = CurrentLevel.Id;
        if (_bestMovesByLevelId.TryGetValue(id, out var best) && best <= Moves) return false;
        _bestMovesByLevelId[id] = Moves;
        return true;
    }

    private bool TryRestoreSavedState(GameProgressDocument document)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(document.CurrentStatePattern))
            {
                _game.SetLevel(document.CurrentLevelIndex);
                return false;
            }

            var snapshot = new ChapterSessionSnapshot(
                document.CurrentLevelIndex,
                ChainState.FromPattern(document.CurrentStatePattern),
                document.CurrentMoves,
                document.UndoHistory.Select(frame => new ChapterHistorySnapshot(
                    ChainState.FromPattern(frame.StatePattern),
                    frame.Moves)).ToArray(),
                document.RedoHistory.Select(frame => new ChapterHistorySnapshot(
                    ChainState.FromPattern(frame.StatePattern),
                    frame.Moves)).ToArray());

            return _game.TryRestoreSession(snapshot);
        }
        catch
        {
            _game.ResetLevel();
            return false;
        }
    }

    private void LoadProgress()
    {
        var doc = _progressStore.Load();
        var knownIds = _game.Levels.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in doc.CompletedLevelIds)
            if (knownIds.Contains(id)) _game.CompletedLevelIds.Add(id);
        foreach (var entry in doc.BestMovesByLevelId)
            if (knownIds.Contains(entry.Key) && entry.Value > 0) _bestMovesByLevelId[entry.Key] = entry.Value;

        var shouldRestoreSession = doc.CurrentLevelIndex != 0
            || _game.CompletedLevelIds.Count > 0
            || _bestMovesByLevelId.Count > 0
            || doc.CurrentMoves > 0
            || !string.IsNullOrWhiteSpace(doc.CurrentStatePattern);

        if (shouldRestoreSession)
        {
            var restoredState = TryRestoreSavedState(doc);
            StatusMessage = restoredState
                ? $"Run restored on {CurrentLevel.Subtitle}."
                : "Progress restored.";
        }

        var persistedSolvedState = false;
        if (_game.IsSolved)
        {
            persistedSolvedState |= _game.CompletedLevelIds.Add(CurrentLevel.Id);
            persistedSolvedState |= RecordBestRun();
            if (persistedSolvedState)
            {
                SaveProgress();
            }
        }

        _hasSavedProgress = HasSavedRunData;
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;

        try
        {
            var doc = _settingsStore.Load();
            SoundEnabled = doc.SoundEnabled;
            AnimationSpeed = doc.AnimationSpeed;
            ShowHintHighlights = doc.ShowHintHighlights;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    partial void OnSoundEnabledChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnAnimationSpeedChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
        {
            AnimationSpeed = clamped;
            return;
        }

        if (_isLoadingSettings)
        {
            return;
        }

        SaveSettings();
    }

    partial void OnShowHintHighlightsChanged(bool value)
    {
        if (!value && SelectedJointIndex is not null)
        {
            SelectedJointIndex = null;
        }

        if (_isLoadingSettings)
        {
            return;
        }

        SaveSettings();
        RefreshAll();
    }

    private void SaveProgress()
    {
        var snapshot = _game.CreateSessionSnapshot();
        _progressStore.Save(new GameProgressDocument(
            GameProgressStore.CurrentVersion,
            snapshot.LevelIndex,
            _game.CompletedLevelIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            new Dictionary<string, int>(_bestMovesByLevelId, StringComparer.Ordinal),
            snapshot.CurrentState.SerializeSegments(),
            snapshot.Moves,
            snapshot.UndoHistory.Select(frame => new SavedHistoryStateDocument(
                frame.State.SerializeSegments(),
                frame.Moves)).ToArray(),
            snapshot.RedoHistory.Select(frame => new SavedHistoryStateDocument(
                frame.State.SerializeSegments(),
                frame.Moves)).ToArray()));
        _hasSavedProgress = HasSavedRunData;
    }

    /// <summary>Refreshes all derived HUD properties.</summary>
    public void RefreshAll()
    {
        var level = CurrentLevel;
        var hasBest = _bestMovesByLevelId.TryGetValue(level.Id, out var bestMoves);

        Title = level.Title;
        Subtitle = level.Subtitle;
        Description = level.Description;
        AccentHex = level.AccentHex;
        ProgressText = $"{LevelIndex + 1}/{Levels.Count}";
        CompletedText = $"{_game.CompletedLevelIds.Count}/{Levels.Count}";
        MovesText = Moves.ToString(CultureInfo.InvariantCulture);
        BestText = hasBest ? $"{bestMoves} / {level.OptimalMoves}" : $"- / {level.OptimalMoves}";
        BadgeText = BuildBadgeText(level.OptimalMoves, hasBest, bestMoves);
        ApplyBadgeStyle(hasBest, bestMoves);

        var solvedStatus = IsSolved && !IsBusy
            ? BuildSolvedStatus(level.OptimalMoves, hasBest, bestMoves)
            : StatusMessage;
        StatusText = SelectedJointIndex is null
            ? solvedStatus
            : $"{solvedStatus} Selected joint: {SelectedJointIndex}.";

        ShowSolvedCard = IsSolved && !IsBusy;
        if (ShowSolvedCard)
        {
            SolvedTitle = LevelIndex == Levels.Count - 1 ? "Puzzle Set Complete" : $"{Subtitle} Solved";
            SolvedSummary = BuildSolvedCardSummary(level.OptimalMoves, hasBest, bestMoves);
            CanAdvance = LevelIndex < Levels.Count - 1;
        }

        UpdateHomeOverlayProperties();
        UpdateChapterPickerItems();

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanRotateManually));
        OnPropertyChanged(nameof(IsSolved));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(LevelIndex));
        OnPropertyChanged(nameof(Moves));
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(CurrentLevel));
    }

    private void UpdateHomeOverlayProperties()
    {
        if (!IsHomeVisible) return;

        var hasRun = _hasSavedProgress || HasSavedRunData;
        var current = $"{LevelIndex + 1}/{Levels.Count}: {Subtitle}";

        HomeTitle = HomeAllowsClose ? "Pause Menu"
            : hasRun ? "Continue Your Run" : "Start A New Run";

        HomeSummary = HomeAllowsClose
            ? $"Current chapter: {current}. Resume, jump chapters, or wipe the run and start clean."
            : hasRun
                ? $"Progress is loaded and ready. Continue from {current}, or wipe the board and begin from Chapter 1."
                : "A fresh puzzle run is ready. Learn the chain on the early boards, then chase par on the later shapes.";

        HomeProgressInfo = $"{_game.CompletedLevelIds.Count}/{Levels.Count} chapters cleared\nCurrent: {current}";
        HomeBestInfo = _bestMovesByLevelId.Count > 0
            ? $"{_bestMovesByLevelId.Count} chapters have saved best runs\nPar is tracked on every chapter"
            : "No best runs stored yet\nPar is tracked on every chapter";

        var gold = 0;
        var silver = 0;
        var bronze = 0;
        foreach (var level in Levels)
        {
            if (!_bestMovesByLevelId.TryGetValue(level.Id, out var best)) continue;
            var delta = best - level.OptimalMoves;
            if (delta <= 0) gold++;
            else if (delta == 1) silver++;
            else bronze++;
        }
        HomeMedalInfo = gold + silver + bronze > 0
            ? $"🥇 {gold}  🥈 {silver}  🥉 {bronze}\n{gold + silver + bronze}/{Levels.Count} chapters rated"
            : "No medals earned yet\nPar or better = gold";

        HomePrimaryLabel = HomeAllowsClose ? "Resume" : hasRun ? "Continue" : "Start Game";
        HomeSecondaryVisible = HomeAllowsClose || hasRun;
    }

    private void UpdateChapterPickerItems()
    {
        ChapterPickerItems = Levels.Select((level, index) =>
        {
            var marker = _game.CompletedLevelIds.Contains(level.Id) ? "✓" : " ";
            var bestSuffix = _bestMovesByLevelId.TryGetValue(level.Id, out var best)
                ? $"  best {best}/{level.OptimalMoves}" : "";
            return $"{marker} {index + 1:00}. {level.Subtitle}{bestSuffix}";
        }).ToArray();
        ChapterPickerSelectedIndex = LevelIndex;
    }

    private string BuildBadgeText(int optimal, bool hasBest, int best)
    {
        if (IsSolved && !IsBusy && hasBest && Moves == best)
            return Moves == optimal ? "Personal best at par" : "Personal best";
        if (IsSolved && !IsBusy)
            return Moves == optimal ? "Chapter cleared at par" : $"Chapter cleared, par {optimal}";
        if (hasBest)
            return $"Par {optimal}, best {best}";
        return _game.CompletedLevelIds.Contains(CurrentLevel.Id)
            ? $"Cleared before, par {optimal}" : $"Fresh chapter, par {optimal}";
    }

    private void ApplyBadgeStyle(bool hasBest, int best)
    {
        if (IsSolved && !IsBusy && hasBest && Moves == best)
        {
            BadgeBg = "#DCFCE7"; BadgeBorder = "#86EFAC"; BadgeFg = "#166534";
        }
        else if (IsSolved && !IsBusy)
        {
            BadgeBg = "#FEF3C7"; BadgeBorder = "#FCD34D"; BadgeFg = "#92400E";
        }
        else
        {
            BadgeBg = $"#1E{AccentHex.TrimStart('#')}";
            BadgeBorder = $"#42{AccentHex.TrimStart('#')}";
            BadgeFg = AccentHex;
        }
    }

    private string BuildSolvedCardSummary(int optimal, bool hasBest, int best)
    {
        var bestClause = hasBest
            ? $"Best run: {best} move{(best == 1 ? "" : "s")}." : "First clear recorded.";
        var delta = Moves - optimal;
        var parClause = delta switch
        {
            < 0 => $"You beat par by {-delta} move{(-delta == 1 ? "" : "s")}.",
            0 => "You matched par exactly.",
            _ => $"Par is {optimal}, so this solve was {delta} move{(delta == 1 ? "" : "s")} over."
        };
        var completion = LevelIndex == Levels.Count - 1
            ? $"You have cleared all {Levels.Count} chapters."
            : "Use Next Chapter to keep the run going.";
        return $"Solved in {Moves} move{(Moves == 1 ? "" : "s")}. {parClause} {bestClause} {completion}";
    }

    private string BuildSolvedStatus(int optimal, bool hasBest, int best)
    {
        var delta = Moves - optimal;
        var parText = delta switch { < 0 => $"{-delta} under par", 0 => "matched par", _ => $"{delta} over par" };
        if (!hasBest) return $"Solved in {Moves} moves, {parText}.";
        return Moves == best
            ? $"Solved in {Moves} moves, {parText}. Personal best matched."
            : $"Solved in {Moves} moves, {parText}. Best run is {best}.";
    }

    private static string FormatRotation(int rotation) => rotation < 0 ? "left" : "right";
}
