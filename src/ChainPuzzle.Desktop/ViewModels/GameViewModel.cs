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
    private readonly Dictionary<int, ChainSolver> _analysisSolvers = new();
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    [NotifyPropertyChangedFor(nameof(CanRedo))]
    [NotifyPropertyChangedFor(nameof(CanUseHint))]
    private bool _expertMode;

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
    public IReadOnlyList<ChapterGalleryCard> ChapterCards => BuildChapterCards();
    public ChainLevel CurrentLevel => _game.CurrentLevel;
    public ChainState CurrentState => _game.CurrentState;
    public int LevelIndex => _game.LevelIndex;
    public int Moves => _game.Moves;
    public bool IsSolved => _game.IsSolved;
    public bool CanUndo => !ExpertMode && _game.CanUndo;
    public bool CanRedo => !ExpertMode && _game.CanRedo;
    public bool CanUseHint => !ExpertMode && !IsSolved;

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
    [ObservableProperty] private string _difficultyText = "";
    [ObservableProperty] private string _modeText = "";
    [ObservableProperty] private string _boardReadText = "";
    [ObservableProperty] private string _selectionText = "";
    [ObservableProperty] private string _approachText = "";
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

    private sealed record StateInsight(
        int TargetSize,
        int Overlap,
        int MisplacedCount,
        int LegalMoves,
        int ImprovingMoves,
        int NeutralMoves,
        int RiskyMoves,
        int BestGain,
        int ContainedMoves);

    private sealed record RotationReadout(
        bool IsBlocked,
        int OverlapDelta,
        int LegalMoveDelta,
        bool EndsInsideTarget);

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
        var previousState = CurrentState;
        if (!_game.TryRotate(jointIndex, rotation, out var nextState))
        {
            StatusMessage = BuildBlockedMoveStatus(previousState);
            RefreshAll();
            return false;
        }

        StatusMessage = BuildRotationStatus(jointIndex, rotation, previousState, nextState);
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
        StatusMessage = ExpertMode
            ? _hasSavedProgress
                ? $"Expert run restored. {BuildCompactStateSummary(CurrentState)}"
                : $"Expert run started. {BuildOpeningStatus(CurrentLevel)}"
            : _hasSavedProgress
                ? $"Progress restored. {BuildCompactStateSummary(CurrentState)}"
                : BuildOpeningStatus(CurrentLevel);
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
        if (ExpertMode)
        {
            StatusMessage = "Undo is disabled in expert mode.";
            RefreshAll();
            return;
        }

        if (IsBusy || !_game.TryUndo()) return;
        StatusMessage = $"Step undone. {BuildCompactStateSummary(CurrentState)}";
        SaveProgress();
        RefreshAll();
    }

    [RelayCommand]
    private void Redo()
    {
        if (ExpertMode)
        {
            StatusMessage = "Redo is disabled in expert mode.";
            RefreshAll();
            return;
        }

        if (IsBusy || !_game.TryRedo()) return;
        if (IsSolved) FinalizeSolvedState();
        else StatusMessage = $"Move replayed. {BuildCompactStateSummary(CurrentState)}";
        SaveProgress();
        RefreshAll();
    }

    [RelayCommand]
    private void ResetLevel()
    {
        _game.ResetLevel();
        SelectedJointIndex = null;
        StatusMessage = $"{CurrentLevel.Subtitle} reset. {BuildOpeningStatus(CurrentLevel)}";
        SaveProgress();
        RefreshAll();
    }

    [RelayCommand]
    private async Task FindHint()
    {
        if (ExpertMode)
        {
            StatusMessage = "Nudge is disabled in expert mode.";
            RefreshAll();
            return;
        }

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

            var readout = AnalyzeRotation(CurrentLevel, CurrentState, hint.Value.JointIndex, hint.Value.Rotation);
            StatusMessage = $"Nudge: joint {hint.Value.JointIndex}, rotate {FormatRotation(hint.Value.Rotation)}. {BuildRotationOutcomeText(readout)}";
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

    public void OpenChapterFromHome(int index)
    {
        if (index < 0 || index >= Levels.Count || IsBusy)
        {
            return;
        }

        if (index != LevelIndex)
        {
            ChangeLevel(index);
        }

        HideHome();
        StatusMessage = $"Jumped to {CurrentLevel.Subtitle}. {BuildOpeningStatus(CurrentLevel)}";
        RefreshAll();
    }

    public void SelectJoint(int? jointIndex)
    {
        SelectedJointIndex = jointIndex;
        if (jointIndex.HasValue)
        {
            StatusMessage = $"Joint {jointIndex.Value} selected. Preview both directions before committing.";
        }
        RefreshAll();
    }

    public void SaveSettings()
    {
        _settingsStore.Save(new SettingsDocument(
            SettingsStore.CurrentVersion,
            SoundEnabled,
            AnimationSpeed,
            ShowHintHighlights,
            ExpertMode));
    }

    // ====================
    //  Internal helpers
    // ====================

    private void ChangeLevel(int nextIndex, string? status = null)
    {
        _game.SetLevel(nextIndex);
        SelectedJointIndex = null;
        StatusMessage = status ?? $"{CurrentLevel.Subtitle} loaded. {BuildOpeningStatus(CurrentLevel)}";
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
        StatusMessage = ExpertMode
            ? $"New expert run started. {BuildOpeningStatus(CurrentLevel)}"
            : $"New run started. {BuildOpeningStatus(CurrentLevel)}";
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
            ExpertMode = doc.ExpertMode;
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

    partial void OnExpertModeChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        if (value && SelectedJointIndex is not null)
        {
            SelectedJointIndex = null;
        }

        SaveSettings();
        StatusMessage = value
            ? "Expert mode enabled. Undo, redo, and nudge are disabled."
            : "Expert mode disabled. Assist tools are available again.";
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
        var difficultyLabel = BuildDifficultyLabel(level);
        var difficultyScoreText = BuildDifficultyScoreText(level);
        var insight = AnalyzeState(level, CurrentState);

        Title = level.Title;
        Subtitle = level.Subtitle;
        Description = level.Description;
        AccentHex = level.AccentHex;
        ProgressText = $"{LevelIndex + 1}/{Levels.Count}";
        CompletedText = $"{_game.CompletedLevelIds.Count}/{Levels.Count}";
        MovesText = Moves.ToString(CultureInfo.InvariantCulture);
        BestText = hasBest ? $"{bestMoves} / {level.OptimalMoves}" : $"- / {level.OptimalMoves}";
        DifficultyText = $"{difficultyLabel} • {difficultyScoreText}";
        ModeText = ExpertMode ? "Expert • assist off" : "Standard • assist on";
        BoardReadText = BuildBoardReadText(insight);
        SelectionText = SelectedJointIndex is null
            ? BuildSelectionPrompt(insight)
            : BuildSelectedJointText(level, CurrentState, SelectedJointIndex.Value);
        ApproachText = BuildApproachText(level);
        BadgeText = BuildBadgeText(level, hasBest, bestMoves);
        ApplyBadgeStyle(hasBest, bestMoves);

        var solvedStatus = IsSolved && !IsBusy
            ? BuildSolvedStatus(level.OptimalMoves, hasBest, bestMoves)
            : StatusMessage;
        StatusText = solvedStatus;

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
        OnPropertyChanged(nameof(CanUseHint));
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
            ? $"Current chapter: {current}. Resume, jump with the gallery cards, or wipe the run and start clean. {(ExpertMode ? "Expert mode is active." : "Standard mode is active.")}"
            : hasRun
                ? $"Progress is loaded and ready. Continue from {current}, or jump to another chapter from the gallery. {(ExpertMode ? "Expert mode is active." : "Standard mode is active.")}"
                : ExpertMode
                    ? "An expert run is ready. Assist tools are off, so the puzzle tree has to do the teaching."
                    : "A fresh puzzle run is ready. Learn the chain on the early boards, then chase par on the later shapes from the gallery.";

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
            ? $"Gold {gold}  Silver {silver}  Bronze {bronze}\n{gold + silver + bronze}/{Levels.Count} chapters rated"
            : "No medals earned yet\nPar or better = gold";

        HomePrimaryLabel = HomeAllowsClose ? "Resume"
            : hasRun ? "Continue"
            : ExpertMode ? "Start Expert Run" : "Start Game";
        HomeSecondaryVisible = HomeAllowsClose || hasRun;
    }

    private ChapterGalleryCard[] BuildChapterCards()
    {
        return Levels.Select((level, index) =>
        {
            var hasBest = _bestMovesByLevelId.TryGetValue(level.Id, out var bestMoves);
            var bestText = hasBest
                ? $"Best {bestMoves}/{level.OptimalMoves}"
                : _game.CompletedLevelIds.Contains(level.Id)
                    ? $"Cleared above par {level.OptimalMoves}"
                    : $"No clear yet";

            var profile = level.TreeProfile;
            var difficultyText = $"{BuildDifficultyLabel(level)} • {BuildDifficultyScoreText(level)}";
            var methodText = BuildMethodHint(level);
            var pressureText = profile is null
                ? $"Par {level.OptimalMoves}"
                : $"Par {level.OptimalMoves} • {profile.StartCloserMoveCount}/{profile.StartLegalMoveCount} openings improve coverage • {profile.StartTrapMoveCount} trap turns";
            var branchText = profile is null
                ? "No branch profile baked in"
                : $"{profile.StartFalseProgressMoveCount} false-fit starts • {profile.NearTargetDecoyCount} near-target decoys • shell-4 breadth {profile.GoalShellCounts[^1]}";

            return new ChapterGalleryCard(
                index,
                $"{index + 1:00}",
                level.Subtitle,
                level.AccentHex,
                level.TargetPoints,
                index == LevelIndex,
                _game.CompletedLevelIds.Contains(level.Id),
                BuildMedalLabel(level.OptimalMoves, hasBest, bestMoves),
                difficultyText,
                methodText,
                bestText,
                pressureText,
                branchText);
        }).ToArray();
    }

    private void UpdateChapterPickerItems()
    {
        ChapterPickerItems = Levels.Select((level, index) =>
        {
            var marker = _game.CompletedLevelIds.Contains(level.Id) ? "✓" : " ";
            var difficultySuffix = $"  [{BuildDifficultyLabel(level)} {BuildDifficultyScoreText(level)}]";
            var bestSuffix = _bestMovesByLevelId.TryGetValue(level.Id, out var best)
                ? $"  best {best}/{level.OptimalMoves}" : "";
            return $"{marker} {index + 1:00}. {level.Subtitle}{difficultySuffix}{bestSuffix}";
        }).ToArray();
        ChapterPickerSelectedIndex = LevelIndex;
    }

    private static string BuildMedalLabel(int optimal, bool hasBest, int best)
    {
        if (!hasBest)
        {
            return "Open";
        }

        var delta = best - optimal;
        if (delta <= 0)
        {
            return "Gold";
        }

        return delta == 1 ? "Silver" : "Bronze";
    }

    private string BuildBadgeText(ChainLevel level, bool hasBest, int best)
    {
        var difficultyLabel = BuildDifficultyLabel(level);
        var modePrefix = ExpertMode ? $"Expert / {difficultyLabel}" : difficultyLabel;
        var scoreText = BuildDifficultyScoreText(level);

        if (IsSolved && !IsBusy && hasBest && Moves == best)
            return Moves == level.OptimalMoves ? $"{modePrefix} run • heat {scoreText} • personal best at par" : $"{modePrefix} run • heat {scoreText} • personal best";
        if (IsSolved && !IsBusy)
            return Moves == level.OptimalMoves ? $"{modePrefix} clear • heat {scoreText} • at par" : $"{modePrefix} clear • heat {scoreText} • par {level.OptimalMoves}";
        if (hasBest)
            return $"{modePrefix} • heat {scoreText} • par {level.OptimalMoves} • best {best}";
        return _game.CompletedLevelIds.Contains(CurrentLevel.Id)
            ? $"{modePrefix} • heat {scoreText} • cleared before • par {level.OptimalMoves}"
            : $"{modePrefix} • heat {scoreText} • fresh chapter • par {level.OptimalMoves}";
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
        var difficultyClause = $"Difficulty: {BuildDifficultyLabel(CurrentLevel)} ({BuildDifficultyScoreText(CurrentLevel)} heat). {BuildApproachText(CurrentLevel)}";
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
        var modeClause = ExpertMode ? "This was an expert run." : "This was a standard run.";
        return $"Solved in {Moves} move{(Moves == 1 ? "" : "s")}. {parClause} {bestClause} {difficultyClause} {modeClause} {completion}";
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

    private static string BuildDifficultyLabel(ChainLevel level)
    {
        var score = BuildDifficultyScore(level);
        return score switch
        {
            < 56 => "Tactical",
            < 64 => "Tight",
            < 72 => "Demanding",
            < 80 => "Brutal",
            _ => "Savage"
        };
    }

    private static string BuildDifficultyScoreText(ChainLevel level)
    {
        return $"{BuildDifficultyScore(level)}/100";
    }

    private static int BuildDifficultyScore(ChainLevel level)
    {
        var profile = level.TreeProfile;
        return profile is null
            ? Math.Clamp(level.OptimalMoves * 10, 0, 100)
            : ComputeDifficultyScore(level.OptimalMoves, profile);
    }

    private static int ComputeDifficultyScore(int optimalMoves, LevelTreeProfile profile)
    {
        var legalMoves = Math.Max(profile.StartLegalMoveCount, 1);
        var trapRatio = profile.StartTrapMoveCount / (double)legalMoves;
        var falseRatio = profile.StartFalseProgressMoveCount / (double)legalMoves;
        var shellBreadth = profile.GoalShellCounts.Count >= 5 ? profile.GoalShellCounts[4] : 3_000;

        var score = 24d
                    + ((optimalMoves - 6) * 12d)
                    + (Normalize(trapRatio, 0.65d, 0.90d) * 14d)
                    + (Normalize(falseRatio, 0.55d, 0.90d) * 12d)
                    + (Normalize(profile.NearTargetDecoyCount, 8d, 30d) * 10d)
                    + (Normalize(shellBreadth, 2_500d, 5_500d) * 8d)
                    + (Normalize(6 - profile.StartOverlap, 0d, 3d) * 4d)
                    + (Normalize(6 - profile.StartCloserMoveCount, 0d, 5d) * 4d);

        return (int)Math.Round(Math.Clamp(score, 0d, 100d));
    }

    private static string BuildMethodHint(ChainLevel level)
    {
        var profile = level.TreeProfile;
        if (profile is null)
        {
            return "cover the silhouette directly";
        }

        var legalMoves = Math.Max(profile.StartLegalMoveCount, 1);
        var falseLaneRatio = profile.StartFalseProgressMoveCount / (double)legalMoves;
        var trapRatio = profile.StartTrapMoveCount / (double)legalMoves;

        return falseLaneRatio >= 0.75
            ? "Filter the false fits"
            : trapRatio >= 0.7
                ? "Keep the escape lanes alive"
                : profile.StartCloserMoveCount <= 3
                    ? "Rebuild the center first"
                    : "Trade local fit for final order";
    }

    private static string BuildApproachText(ChainLevel level)
    {
        var profile = level.TreeProfile;
        if (profile is null)
        {
            return "Cover the silhouette exactly.";
        }

        var methodHint = BuildMethodHint(level);

        var routeHint = profile.StartCloserMoveCount switch
        {
            <= 2 => $"Only {profile.StartCloserMoveCount} of {profile.StartLegalMoveCount} opening turns improve coverage. Expect a slow read.",
            <= 4 => $"{profile.StartCloserMoveCount} of {profile.StartLegalMoveCount} opening turns improve coverage, so read before you commit.",
            _ => $"{profile.StartCloserMoveCount} of {profile.StartLegalMoveCount} opening turns improve coverage, but the finish order is still the real constraint."
        };

        return $"Method: {methodHint}. {routeHint} Pressure profile: {profile.StartTrapMoveCount} trap starts, {profile.StartFalseProgressMoveCount} false-fit starts, {profile.NearTargetDecoyCount} near-target decoys.";
    }

    private StateInsight AnalyzeState(ChainLevel level, ChainState state)
    {
        var overlap = level.CountTargetOverlap(state);
        var legalMoves = GetAnalysisSolver(state.SegmentCount).GetLegalMoves(state);
        var improvingMoves = 0;
        var neutralMoves = 0;
        var riskyMoves = 0;
        var bestGain = 0;
        var containedMoves = 0;

        foreach (var legalMove in legalMoves)
        {
            var nextOverlap = level.CountTargetOverlap(legalMove.NextState);
            var delta = nextOverlap - overlap;
            bestGain = Math.Max(bestGain, delta);

            if (delta > 0)
            {
                improvingMoves += 1;
            }
            else if (delta < 0)
            {
                riskyMoves += 1;
            }
            else
            {
                neutralMoves += 1;
            }

            if (level.IsWithinTarget(legalMove.NextState))
            {
                containedMoves += 1;
            }
        }

        return new StateInsight(
            level.TargetPoints.Count,
            overlap,
            level.TargetPoints.Count - overlap,
            legalMoves.Count,
            improvingMoves,
            neutralMoves,
            riskyMoves,
            bestGain,
            containedMoves);
    }

    private RotationReadout AnalyzeRotation(ChainLevel level, ChainState state, int jointIndex, int rotation)
    {
        var nextState = state.RotateFromJoint(jointIndex, rotation);
        if (nextState is null)
        {
            return new RotationReadout(true, 0, 0, false);
        }

        var currentOverlap = level.CountTargetOverlap(state);
        var nextOverlap = level.CountTargetOverlap(nextState);
        var currentLegalMoves = GetAnalysisSolver(state.SegmentCount).GetLegalMoves(state).Count;
        var nextLegalMoves = GetAnalysisSolver(nextState.SegmentCount).GetLegalMoves(nextState).Count;

        return new RotationReadout(
            false,
            nextOverlap - currentOverlap,
            nextLegalMoves - currentLegalMoves,
            level.IsWithinTarget(nextState));
    }

    private ChainSolver GetAnalysisSolver(int segmentCount)
    {
        if (_analysisSolvers.TryGetValue(segmentCount, out var existing))
        {
            return existing;
        }

        var solver = new ChainSolver(segmentCount);
        _analysisSolvers[segmentCount] = solver;
        return solver;
    }

    private static string BuildBoardReadText(StateInsight insight)
    {
        var percent = insight.TargetSize == 0
            ? 0
            : (int)Math.Round((insight.Overlap * 100d) / insight.TargetSize);
        var improvementText = insight.BestGain > 0
            ? $"Best immediate gain: +{insight.BestGain} tile{(insight.BestGain == 1 ? "" : "s")}."
            : insight.ImprovingMoves == 0
                ? "No turn improves coverage immediately."
                : "Coverage gains are shallow from here.";

        return $"{insight.Overlap}/{insight.TargetSize} tiles aligned ({percent}%). {insight.MisplacedCount} tiles are still out of place. {insight.LegalMoves} legal turns remain; {insight.ImprovingMoves} improve coverage, {insight.RiskyMoves} lose ground, and {insight.ContainedMoves} stay fully inside the silhouette. {improvementText}";
    }

    private string BuildSelectionPrompt(StateInsight insight)
    {
        if (IsSolved)
        {
            return "Board complete. Use Replay Chapter to refine the line or Next Chapter to keep climbing.";
        }

        return insight.BestGain > 0
            ? $"Select a joint to preview left and right. The best available turn gains {insight.BestGain} tile{(insight.BestGain == 1 ? "" : "s")} immediately."
            : "Select a joint to preview left and right. This position is about ordering the folds, not grabbing instant coverage.";
    }

    private string BuildSelectedJointText(ChainLevel level, ChainState state, int jointIndex)
    {
        var left = AnalyzeRotation(level, state, jointIndex, -1);
        var right = AnalyzeRotation(level, state, jointIndex, 1);
        return $"Joint {jointIndex}. Left: {BuildRotationOutcomeText(left)} Right: {BuildRotationOutcomeText(right)}";
    }

    private string BuildCompactStateSummary(ChainState state)
    {
        var insight = AnalyzeState(CurrentLevel, state);
        return $"{insight.Overlap}/{insight.TargetSize} aligned, {insight.MisplacedCount} still misplaced.";
    }

    private static string BuildOpeningStatus(ChainLevel level)
    {
        var profile = level.TreeProfile;
        if (profile is null)
        {
            return "Pick a joint and start matching the silhouette.";
        }

        return $"{BuildMethodHint(level)}. Only {profile.StartCloserMoveCount} of {profile.StartLegalMoveCount} opening turns improve coverage.";
    }

    private string BuildBlockedMoveStatus(ChainState state)
    {
        var insight = AnalyzeState(CurrentLevel, state);
        return insight.ImprovingMoves == 0
            ? "That hinge collides with the chain. This position is about order, not forcing a tighter fold."
            : $"That hinge collides with the chain. {insight.ImprovingMoves} legal turn{(insight.ImprovingMoves == 1 ? "" : "s")} still improve coverage from here.";
    }

    private string BuildRotationStatus(int jointIndex, int rotation, ChainState previousState, ChainState nextState)
    {
        var previousOverlap = CurrentLevel.CountTargetOverlap(previousState);
        var nextOverlap = CurrentLevel.CountTargetOverlap(nextState);
        var delta = nextOverlap - previousOverlap;
        var insight = AnalyzeState(CurrentLevel, nextState);
        var deltaText = delta switch
        {
            > 0 => $"coverage +{delta}",
            < 0 => $"coverage {delta}",
            _ => "coverage unchanged"
        };

        return $"Joint {jointIndex} {FormatRotation(rotation)}. {deltaText}; now {insight.Overlap}/{insight.TargetSize} aligned with {insight.LegalMoves} legal turns left.";
    }

    private static string BuildRotationOutcomeText(RotationReadout readout)
    {
        if (readout.IsBlocked)
        {
            return "blocked.";
        }

        var coverageText = readout.OverlapDelta switch
        {
            > 0 => $"coverage +{readout.OverlapDelta}",
            < 0 => $"coverage {readout.OverlapDelta}",
            _ => "coverage flat"
        };
        var tempoText = readout.LegalMoveDelta switch
        {
            > 0 => $", legal turns +{readout.LegalMoveDelta}",
            < 0 => $", legal turns {readout.LegalMoveDelta}",
            _ => ", legal turns unchanged"
        };
        var containmentText = readout.EndsInsideTarget
            ? ", stays inside target."
            : ", still reaches outside the silhouette.";

        return $"{coverageText}{tempoText}{containmentText}";
    }

    private static string FormatRotation(int rotation) => rotation < 0 ? "left" : "right";

    private static double Normalize(double value, double minimum, double maximum)
    {
        if (maximum <= minimum)
        {
            return 0d;
        }

        return Math.Clamp((value - minimum) / (maximum - minimum), 0d, 1d);
    }
}
