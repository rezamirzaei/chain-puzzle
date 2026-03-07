using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ChainPuzzle.Core;
using ChainPuzzle.Desktop.Controls;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace ChainPuzzle.Desktop;

public partial class MainWindow : Window
{
    private sealed record RotationAnimation(
        ChainState FromState,
        ChainState ToState,
        int JointIndex,
        int Rotation,
        TimeSpan StartedAt,
        TimeSpan Duration);

    private sealed record DragState(int JointIndex, double LastAngle, Point LastPoint);

    private readonly ChapterGame _game;
    private readonly GameProgressStore _progressStore = new();
    private readonly Dictionary<string, int> _bestMovesByLevelId = new(StringComparer.Ordinal);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _frameTimer;

    private readonly ChainBoardControl _board;
    private readonly Border _homeOverlay;
    private readonly Border _validationBadge;
    private readonly Border _solvedCard;
    private readonly TextBlock _homeTitleText;
    private readonly TextBlock _homeSummaryText;
    private readonly TextBlock _homeProgressText;
    private readonly TextBlock _homeBestText;
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _progressText;
    private readonly TextBlock _completedText;
    private readonly TextBlock _movesText;
    private readonly TextBlock _bestText;
    private readonly TextBlock _validationText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _solvedTitleText;
    private readonly TextBlock _solvedSummaryText;
    private readonly ComboBox _chapterPicker;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _menuButton;
    private readonly Button _resetButton;
    private readonly Button _hintButton;
    private readonly Button _rotateLeftButton;
    private readonly Button _rotateRightButton;
    private readonly Button _homePrimaryButton;
    private readonly Button _homeSecondaryButton;
    private readonly Button _homeCloseButton;
    private readonly Button _solvedNextButton;

    private RotationAnimation? _animation;
    private DragState? _dragState;
    private int? _selectedJointIndex;
    private bool _isFindingHint;
    private bool _isSyncingChapterPicker;
    private bool _homeOverlayAllowsClose;
    private bool _hasSavedProgress;
    private string _statusMessage = "Drag a joint to rotate the chain and fit the target shape.";

    public MainWindow()
    {
        InitializeComponent();

        _board = GetRequiredControl<ChainBoardControl>("Board");
        _homeOverlay = GetRequiredControl<Border>("HomeOverlay");
        _validationBadge = GetRequiredControl<Border>("ValidationBadge");
        _solvedCard = GetRequiredControl<Border>("SolvedCard");
        _homeTitleText = GetRequiredControl<TextBlock>("HomeTitleText");
        _homeSummaryText = GetRequiredControl<TextBlock>("HomeSummaryText");
        _homeProgressText = GetRequiredControl<TextBlock>("HomeProgressText");
        _homeBestText = GetRequiredControl<TextBlock>("HomeBestText");
        _titleText = GetRequiredControl<TextBlock>("TitleText");
        _subtitleText = GetRequiredControl<TextBlock>("SubtitleText");
        _descriptionText = GetRequiredControl<TextBlock>("DescriptionText");
        _progressText = GetRequiredControl<TextBlock>("ProgressText");
        _completedText = GetRequiredControl<TextBlock>("CompletedText");
        _movesText = GetRequiredControl<TextBlock>("MovesText");
        _bestText = GetRequiredControl<TextBlock>("BestText");
        _validationText = GetRequiredControl<TextBlock>("ValidationText");
        _statusText = GetRequiredControl<TextBlock>("StatusText");
        _solvedTitleText = GetRequiredControl<TextBlock>("SolvedTitleText");
        _solvedSummaryText = GetRequiredControl<TextBlock>("SolvedSummaryText");
        _chapterPicker = GetRequiredControl<ComboBox>("ChapterPicker");
        _previousButton = GetRequiredControl<Button>("PreviousButton");
        _nextButton = GetRequiredControl<Button>("NextButton");
        _undoButton = GetRequiredControl<Button>("UndoButton");
        _redoButton = GetRequiredControl<Button>("RedoButton");
        _menuButton = GetRequiredControl<Button>("MenuButton");
        _resetButton = GetRequiredControl<Button>("ResetButton");
        _hintButton = GetRequiredControl<Button>("HintButton");
        _rotateLeftButton = GetRequiredControl<Button>("RotateLeftButton");
        _rotateRightButton = GetRequiredControl<Button>("RotateRightButton");
        _homePrimaryButton = GetRequiredControl<Button>("HomePrimaryButton");
        _homeSecondaryButton = GetRequiredControl<Button>("HomeSecondaryButton");
        _homeCloseButton = GetRequiredControl<Button>("HomeCloseButton");
        _solvedNextButton = GetRequiredControl<Button>("SolvedNextButton");

        _game = new ChapterGame(ChapterFactory.CreateChapters());
        _frameTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _frameTimer.Tick += OnFrameTimerTick;
        Closed += (_, _) => SaveProgress();

        LoadProgress();
        ShowHomeOverlay(allowClose: false);
        RefreshHud();
        RenderFrame();
    }

    private bool IsBusy => _animation is not null || _isFindingHint;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private T GetRequiredControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name)
               ?? throw new InvalidOperationException($"Required control \"{name}\" was not found.");
    }

    private void RenderFrame()
    {
        UpdateAnimationState();

        var chainPoints = GetDisplayPoints();
        var jointPoints = GetDisplayJointPoints(chainPoints);
        var segmentSpans = _game.CurrentState.GetSegmentSpans();
        _board.UpdateScene(
            chainPoints,
            jointPoints,
            segmentSpans,
            _game.CurrentLevel.TargetPoints,
            _selectedJointIndex,
            _game.CurrentLevel.AccentHex,
            _game.IsSolved && !IsBusy);
    }

    private void OnFrameTimerTick(object? sender, EventArgs e)
    {
        if (_animation is null)
        {
            _frameTimer.Stop();
            return;
        }

        RenderFrame();
    }

    private void UpdateAnimationState()
    {
        if (_animation is null)
        {
            return;
        }

        var elapsed = _clock.Elapsed - _animation.StartedAt;
        if (elapsed < _animation.Duration)
        {
            return;
        }

        _animation = null;
        if (_game.IsSolved)
        {
            FinalizeSolvedState();
        }

        RefreshHud();
    }

    private IReadOnlyList<Point> GetDisplayPoints()
    {
        if (_animation is null)
        {
            return _game.CurrentState
                .GetPoints()
                .Select(point => new Point(point.X, point.Y))
                .ToArray();
        }

        var elapsed = _clock.Elapsed - _animation.StartedAt;
        var progress = Math.Clamp(
            elapsed.TotalMilliseconds / _animation.Duration.TotalMilliseconds,
            0d,
            1d);
        var eased = EaseInOutCubic(progress);
        var angle = _animation.Rotation * (Math.PI / 3d) * eased;

        var sourcePoints = _animation.FromState.GetPoints();
        var pivotIndex = _animation.FromState.GetPointIndexForJoint(_animation.JointIndex);
        var pivot = sourcePoints[pivotIndex];
        var pivotPoint = new Point(pivot.X, pivot.Y);

        var animated = new Point[sourcePoints.Count];
        for (var index = 0; index < sourcePoints.Count; index += 1)
        {
            var source = new Point(sourcePoints[index].X, sourcePoints[index].Y);
            animated[index] = index < pivotIndex
                ? source
                : RotateAround(source, pivotPoint, angle);
        }

        return animated;
    }

    private IReadOnlyList<Point> GetDisplayJointPoints(IReadOnlyList<Point> chainPoints)
    {
        var jointIndices = _game.CurrentState.GetJointPointIndices();
        var jointPoints = new Point[jointIndices.Count];
        for (var index = 0; index < jointIndices.Count; index += 1)
        {
            jointPoints[index] = chainPoints[jointIndices[index]];
        }

        return jointPoints;
    }

    private void RefreshHud()
    {
        var level = _game.CurrentLevel;
        var accentColor = ParseColor(level.AccentHex, Colors.SteelBlue);
        var hasBestRun = _bestMovesByLevelId.TryGetValue(level.Id, out var bestMoves);
        var canInteract = !IsBusy && !_homeOverlay.IsVisible;

        _titleText.Text = level.Title;
        _subtitleText.Text = level.Subtitle;
        _descriptionText.Text = level.Description;

        _progressText.Text = $"{_game.LevelIndex + 1}/{_game.Levels.Count}";
        _completedText.Text = $"{_game.CompletedLevelIds.Count}/{_game.Levels.Count}";
        _movesText.Text = _game.Moves.ToString(CultureInfo.InvariantCulture);
        _bestText.Text = FormatBestAndParText(level.OptimalMoves, hasBestRun, bestMoves);

        _validationText.Text = BuildBadgeText(level.OptimalMoves, hasBestRun, bestMoves);
        ApplyBadgeStyle(hasBestRun, bestMoves, accentColor);

        var solvedStatus = _game.IsSolved && !IsBusy
            ? BuildSolvedStatus(level.OptimalMoves, hasBestRun, bestMoves)
            : _statusMessage;
        _statusText.Text = _selectedJointIndex is null
            ? solvedStatus
            : $"{solvedStatus} Selected joint: {_selectedJointIndex}.";

        _previousButton.IsEnabled = canInteract && _game.LevelIndex > 0;
        _nextButton.IsEnabled = canInteract && _game.LevelIndex < _game.Levels.Count - 1;
        _undoButton.IsEnabled = canInteract && _game.CanUndo;
        _redoButton.IsEnabled = canInteract && _game.CanRedo;
        _menuButton.IsEnabled = !IsBusy;
        _resetButton.IsEnabled = canInteract;
        _hintButton.IsEnabled = canInteract && !_game.IsSolved;
        _chapterPicker.IsEnabled = canInteract;

        var canRotateManually = canInteract && !_game.IsSolved && _selectedJointIndex is not null;
        _rotateLeftButton.IsEnabled = canRotateManually;
        _rotateRightButton.IsEnabled = canRotateManually;

        var accent = new SolidColorBrush(accentColor);
        _nextButton.Background = accent;
        _hintButton.Background = accent;
        _rotateLeftButton.Background = accent;
        _rotateRightButton.Background = accent;
        _solvedNextButton.Background = accent;

        UpdateChapterPickerItems();
        UpdateSolvedCard(level.OptimalMoves, hasBestRun, bestMoves);
        UpdateHomeOverlay();
    }

    private bool TryRotate(int jointIndex, int rotation)
    {
        if (IsBusy || _game.IsSolved)
        {
            return false;
        }

        var fromState = _game.CurrentState;
        if (!_game.TryRotate(jointIndex, rotation, out var toState))
        {
            _statusMessage = "Move blocked by chain collision.";
            RefreshHud();
            return false;
        }

        _animation = new RotationAnimation(
            fromState,
            toState,
            jointIndex,
            rotation,
            _clock.Elapsed,
            TimeSpan.FromMilliseconds(220));

        EnsureFrameLoop();
        RenderFrame();
        _statusMessage = $"Moved joint {jointIndex}.";
        RefreshHud();
        return true;
    }

    private void EnsureFrameLoop()
    {
        if (!_frameTimer.IsEnabled)
        {
            _frameTimer.Start();
        }
    }

    private void ChangeLevel(int nextIndex, string? status = null)
    {
        _game.SetLevel(nextIndex);
        _animation = null;
        _isFindingHint = false;
        _dragState = null;
        _selectedJointIndex = null;
        _statusMessage = status ?? $"{_game.CurrentLevel.Subtitle} loaded.";
        SaveProgress();
        RefreshHud();
        RenderFrame();
    }

    private void LoadProgress()
    {
        var document = _progressStore.Load();
        var knownLevelIds = _game.Levels
            .Select(level => level.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var completedLevelId in document.CompletedLevelIds)
        {
            if (knownLevelIds.Contains(completedLevelId))
            {
                _game.CompletedLevelIds.Add(completedLevelId);
            }
        }

        foreach (var entry in document.BestMovesByLevelId)
        {
            if (knownLevelIds.Contains(entry.Key) && entry.Value > 0)
            {
                _bestMovesByLevelId[entry.Key] = entry.Value;
            }
        }

        if (document.CurrentLevelIndex != 0
            || _game.CompletedLevelIds.Count > 0
            || _bestMovesByLevelId.Count > 0)
        {
            _game.SetLevel(document.CurrentLevelIndex);
            _statusMessage = "Progress restored.";
        }

        _hasSavedProgress = document.CurrentLevelIndex != 0
            || _game.CompletedLevelIds.Count > 0
            || _bestMovesByLevelId.Count > 0;

        UpdateChapterPickerItems();
    }

    private void SaveProgress()
    {
        _progressStore.Save(
            new GameProgressDocument(
                GameProgressStore.CurrentVersion,
                _game.LevelIndex,
                _game.CompletedLevelIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                new Dictionary<string, int>(_bestMovesByLevelId, StringComparer.Ordinal)));
        _hasSavedProgress = _game.LevelIndex != 0
            || _game.CompletedLevelIds.Count > 0
            || _bestMovesByLevelId.Count > 0;
    }

    private bool RecordBestRun()
    {
        var levelId = _game.CurrentLevel.Id;
        if (_bestMovesByLevelId.TryGetValue(levelId, out var existingBest) && existingBest <= _game.Moves)
        {
            return false;
        }

        _bestMovesByLevelId[levelId] = _game.Moves;
        return true;
    }

    private void FinalizeSolvedState()
    {
        _selectedJointIndex = null;
        _dragState = null;

        var isNewBest = RecordBestRun();
        _statusMessage = isNewBest
            ? $"{_game.CurrentLevel.Subtitle} solved in {_game.Moves} moves. New personal best."
            : $"{_game.CurrentLevel.Subtitle} solved in {_game.Moves} moves.";
        SaveProgress();
    }

    private void UpdateSolvedCard(int optimalMoves, bool hasBestRun, int bestMoves)
    {
        _solvedCard.IsVisible = _game.IsSolved && !IsBusy;
        if (!_solvedCard.IsVisible)
        {
            return;
        }

        _solvedTitleText.Text = _game.LevelIndex == _game.Levels.Count - 1
            ? "Puzzle Set Complete"
            : $"{_game.CurrentLevel.Subtitle} Solved";
        _solvedSummaryText.Text = BuildSolvedCardSummary(optimalMoves, hasBestRun, bestMoves);
        _solvedNextButton.IsEnabled = _game.LevelIndex < _game.Levels.Count - 1;
        _solvedNextButton.Content = _game.LevelIndex == _game.Levels.Count - 1
            ? "All Cleared"
            : "Next Chapter";
    }

    private void UpdateHomeOverlay()
    {
        if (!_homeOverlay.IsVisible)
        {
            return;
        }

        var hasRunProgress = _hasSavedProgress
            || _game.LevelIndex > 0
            || _game.CompletedLevelIds.Count > 0
            || _bestMovesByLevelId.Count > 0;
        var currentChapter = $"{_game.LevelIndex + 1}/{_game.Levels.Count}: {_game.CurrentLevel.Subtitle}";

        _homeTitleText.Text = _homeOverlayAllowsClose
            ? "Pause Menu"
            : hasRunProgress
                ? "Continue Your Run"
                : "Start A New Run";

        _homeSummaryText.Text = _homeOverlayAllowsClose
            ? $"Current chapter: {currentChapter}. Resume, jump chapters, or wipe the run and start clean."
            : hasRunProgress
                ? $"Progress is loaded and ready. Continue from {currentChapter}, or wipe the board and begin from Chapter 1."
                : "A fresh puzzle run is ready. Learn the chain on the early boards, then chase par on the later shapes.";

        _homeProgressText.Text = $"{_game.CompletedLevelIds.Count}/{_game.Levels.Count} chapters cleared\nCurrent: {currentChapter}";
        _homeBestText.Text = _bestMovesByLevelId.Count > 0
            ? $"{_bestMovesByLevelId.Count} chapters have saved best runs\nPar is tracked on every chapter"
            : "No best runs stored yet\nPar is tracked on every chapter";

        _homePrimaryButton.Content = _homeOverlayAllowsClose
            ? "Resume"
            : hasRunProgress
                ? "Continue"
                : "Start Game";
        _homeSecondaryButton.IsVisible = _homeOverlayAllowsClose || hasRunProgress;
        _homeCloseButton.IsVisible = _homeOverlayAllowsClose;
    }

    private void ShowHomeOverlay(bool allowClose)
    {
        _homeOverlayAllowsClose = allowClose;
        _homeOverlay.IsVisible = true;
        UpdateHomeOverlay();
    }

    private void HideHomeOverlay()
    {
        _homeOverlay.IsVisible = false;
        _homeOverlayAllowsClose = false;
        RefreshHud();
        RenderFrame();
    }

    private void StartNewRun()
    {
        _bestMovesByLevelId.Clear();
        _game.CompletedLevelIds.Clear();
        _game.SetLevel(0);
        _animation = null;
        _isFindingHint = false;
        _dragState = null;
        _selectedJointIndex = null;
        _statusMessage = "New run started.";
        SaveProgress();
        HideHomeOverlay();
    }

    private string BuildSolvedCardSummary(int optimalMoves, bool hasBestRun, int bestMoves)
    {
        var bestClause = hasBestRun
            ? $"Best run: {bestMoves} move{(bestMoves == 1 ? string.Empty : "s")}."
            : "First clear recorded.";
        var parDelta = _game.Moves - optimalMoves;
        var parClause = parDelta switch
        {
            < 0 => $"You beat par by {-parDelta} move{(-parDelta == 1 ? string.Empty : "s")}.",
            0 => "You matched par exactly.",
            _ => $"Par is {optimalMoves}, so this solve was {parDelta} move{(parDelta == 1 ? string.Empty : "s")} over."
        };
        var completionClause = _game.LevelIndex == _game.Levels.Count - 1
            ? $"You have cleared all {_game.Levels.Count} chapters."
            : "Use Next Chapter to keep the run going.";

        return $"Solved in {_game.Moves} move{(_game.Moves == 1 ? string.Empty : "s")}. {parClause} {bestClause} {completionClause}";
    }

    private string BuildSolvedStatus(int optimalMoves, bool hasBestRun, int bestMoves)
    {
        var parDelta = _game.Moves - optimalMoves;
        var parText = parDelta switch
        {
            < 0 => $"{-parDelta} under par",
            0 => "matched par",
            _ => $"{parDelta} over par"
        };

        if (!hasBestRun)
        {
            return $"Solved in {_game.Moves} moves, {parText}.";
        }

        return _game.Moves == bestMoves
            ? $"Solved in {_game.Moves} moves, {parText}. Personal best matched."
            : $"Solved in {_game.Moves} moves, {parText}. Best run is {bestMoves}.";
    }

    private string BuildBadgeText(int optimalMoves, bool hasBestRun, int bestMoves)
    {
        if (_game.IsSolved && !IsBusy && hasBestRun && _game.Moves == bestMoves)
        {
            return _game.Moves == optimalMoves ? "Personal best at par" : "Personal best";
        }

        if (_game.IsSolved && !IsBusy)
        {
            return _game.Moves == optimalMoves ? "Chapter cleared at par" : $"Chapter cleared, par {optimalMoves}";
        }

        if (hasBestRun)
        {
            return $"Par {optimalMoves}, best {bestMoves}";
        }

        return _game.CompletedLevelIds.Contains(_game.CurrentLevel.Id)
            ? $"Cleared before, par {optimalMoves}"
            : $"Fresh chapter, par {optimalMoves}";
    }

    private void ApplyBadgeStyle(bool hasBestRun, int bestMoves, Color accentColor)
    {
        var background = Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B);
        var border = Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B);
        var foreground = accentColor;

        if (_game.IsSolved && !IsBusy && hasBestRun && _game.Moves == bestMoves)
        {
            background = ParseColor("#DCFCE7", Colors.Honeydew);
            border = ParseColor("#86EFAC", Colors.LightGreen);
            foreground = ParseColor("#166534", Colors.DarkGreen);
        }
        else if (_game.IsSolved && !IsBusy)
        {
            background = ParseColor("#FEF3C7", Colors.Bisque);
            border = ParseColor("#FCD34D", Colors.Goldenrod);
            foreground = ParseColor("#92400E", Colors.SaddleBrown);
        }

        _validationBadge.Background = new SolidColorBrush(background);
        _validationBadge.BorderBrush = new SolidColorBrush(border);
        _validationText.Foreground = new SolidColorBrush(foreground);
    }

    private void UpdateChapterPickerItems()
    {
        _isSyncingChapterPicker = true;
        _chapterPicker.ItemsSource = _game.Levels
            .Select((level, index) =>
            {
                var completedMarker = _game.CompletedLevelIds.Contains(level.Id) ? "[x]" : "[ ]";
                var bestSuffix = _bestMovesByLevelId.TryGetValue(level.Id, out var best)
                    ? $"  best {best}/{level.OptimalMoves}"
                    : string.Empty;
                return $"{completedMarker} {index + 1:00}. {level.Subtitle}{bestSuffix}";
            })
            .ToArray();
        _chapterPicker.SelectedIndex = _game.LevelIndex;
        _isSyncingChapterPicker = false;
    }

    private static string FormatBestAndParText(int optimalMoves, bool hasBestRun, int bestMoves)
    {
        return hasBestRun
            ? $"{bestMoves} / {optimalMoves}"
            : $"- / {optimalMoves}";
    }

    private static string FormatRotation(int rotation)
    {
        return rotation < 0 ? "left" : "right";
    }

    private void HomePrimaryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _statusMessage = _hasSavedProgress
            ? "Progress restored."
            : "Pick a joint and start rotating.";
        HideHomeOverlay();
    }

    private void HomeSecondaryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StartNewRun();
    }

    private void HomeCloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HideHomeOverlay();
    }

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeLevel(_game.LevelIndex - 1);
    }

    private void NextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeLevel(_game.LevelIndex + 1);
    }

    private void UndoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsBusy || !_game.TryUndo())
        {
            return;
        }

        _animation = null;
        _dragState = null;
        _statusMessage = "Undid the last move.";
        RefreshHud();
        RenderFrame();
    }

    private void RedoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsBusy || !_game.TryRedo())
        {
            return;
        }

        _animation = null;
        _dragState = null;
        if (_game.IsSolved)
        {
            FinalizeSolvedState();
        }
        else
        {
            _statusMessage = "Replayed the move.";
        }

        RefreshHud();
        RenderFrame();
    }

    private void MenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        ShowHomeOverlay(allowClose: true);
        RefreshHud();
    }

    private void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _game.ResetLevel();
        _animation = null;
        _isFindingHint = false;
        _dragState = null;
        _selectedJointIndex = null;
        _statusMessage = "Chapter reset.";
        RefreshHud();
        RenderFrame();
    }

    private async void HintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsBusy || _game.IsSolved)
        {
            return;
        }

        _isFindingHint = true;
        _statusMessage = "Computing nudge...";
        RefreshHud();

        ChainMove? hint;
        try
        {
            hint = await Task.Run(() => _game.GetHintMove());
        }
        finally
        {
            _isFindingHint = false;
        }

        if (hint is null)
        {
            _statusMessage = "No nudge available right now.";
            RefreshHud();
            return;
        }

        _selectedJointIndex = hint.Value.JointIndex;
        _statusMessage = $"Nudge: try joint {hint.Value.JointIndex}, rotate {FormatRotation(hint.Value.Rotation)}.";
        RefreshHud();
        RenderFrame();
    }

    private void RotateLeftButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedJointIndex is null)
        {
            _statusMessage = "Select a joint first.";
            RefreshHud();
            return;
        }

        TryRotate(_selectedJointIndex.Value, -1);
    }

    private void RotateRightButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedJointIndex is null)
        {
            _statusMessage = "Select a joint first.";
            RefreshHud();
            return;
        }

        TryRotate(_selectedJointIndex.Value, 1);
    }

    private void SolvedResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ResetButton_OnClick(sender, e);
    }

    private void SolvedNextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_game.LevelIndex >= _game.Levels.Count - 1)
        {
            return;
        }

        NextButton_OnClick(sender, e);
    }

    private void ChapterPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingChapterPicker || _chapterPicker.SelectedIndex < 0 || _chapterPicker.SelectedIndex == _game.LevelIndex)
        {
            return;
        }

        ChangeLevel(_chapterPicker.SelectedIndex);
    }

    private void Board_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsBusy || _homeOverlay.IsVisible || _game.IsSolved)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(_board);
        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(_board);
        var jointIndex = _board.HitTestJoint(position);
        if (jointIndex is null)
        {
            _selectedJointIndex = null;
            RefreshHud();
            RenderFrame();
            return;
        }

        _selectedJointIndex = jointIndex;
        _statusMessage = $"Joint {jointIndex.Value} selected.";
        var chainPoints = GetDisplayPoints();
        var pivotPoint = GetJointPoint(chainPoints, jointIndex.Value);
        var pivot = _board.WorldToScreen(pivotPoint);
        var angle = Math.Atan2(position.Y - pivot.Y, position.X - pivot.X);

        _dragState = new DragState(jointIndex.Value, angle, position);
        e.Pointer.Capture(_board);
        RefreshHud();
        RenderFrame();
    }

    private void Board_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragState is null || IsBusy || _homeOverlay.IsVisible)
        {
            return;
        }

        var position = e.GetPosition(_board);
        var chainPoints = GetDisplayPoints();
        var pivotPoint = GetJointPoint(chainPoints, _dragState.JointIndex);
        var pivot = _board.WorldToScreen(pivotPoint);
        var angle = Math.Atan2(position.Y - pivot.Y, position.X - pivot.X);
        var delta = NormalizeAngle(angle - _dragState.LastAngle);
        var horizontalDelta = position.X - _dragState.LastPoint.X;

        if (Math.Abs(delta) < Math.PI / 8d && Math.Abs(horizontalDelta) < 12d)
        {
            return;
        }

        var rotation = Math.Abs(horizontalDelta) > Math.Abs(delta * 48d)
            ? (horizontalDelta > 0 ? 1 : -1)
            : (delta > 0 ? 1 : -1);
        if (TryRotate(_dragState.JointIndex, rotation))
        {
            _dragState = _dragState with
            {
                LastAngle = angle,
                LastPoint = position
            };
        }
    }

    private void Board_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragState = null;
        e.Pointer.Capture(null);
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_homeOverlay.IsVisible)
        {
            if (e.Key == Key.Escape && _homeOverlayAllowsClose)
            {
                HideHomeOverlay();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter or Key.Space)
            {
                HomePrimaryButton_OnClick(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N && _homeSecondaryButton.IsVisible)
            {
                HomeSecondaryButton_OnClick(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            return;
        }

        if (e.Key == Key.Escape)
        {
            _selectedJointIndex = null;
            RefreshHud();
            RenderFrame();
            return;
        }

        if (IsBusy)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var wantsRedo = e.Key == Key.Y || (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            if (e.Key == Key.Z && !wantsRedo)
            {
                UndoButton_OnClick(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (wantsRedo)
            {
                RedoButton_OnClick(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        if (_game.IsSolved)
        {
            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            var minJoint = 1;
            var maxJoint = _game.CurrentState.SegmentCount - 1;
            if (maxJoint < minJoint)
            {
                return;
            }

            if (_selectedJointIndex is null)
            {
                _selectedJointIndex = minJoint;
            }
            else
            {
                var direction = e.Key == Key.Up ? -1 : 1;
                var next = _selectedJointIndex.Value + direction;
                if (next < minJoint)
                {
                    next = maxJoint;
                }
                if (next > maxJoint)
                {
                    next = minJoint;
                }

                _selectedJointIndex = next;
            }

            RefreshHud();
            RenderFrame();
            e.Handled = true;
            return;
        }

        if (_selectedJointIndex is not null && (e.Key == Key.A || e.Key == Key.D))
        {
            var rotation = e.Key == Key.A ? -1 : 1;
            TryRotate(_selectedJointIndex.Value, rotation);
            e.Handled = true;
        }
    }

    private static Point RotateAround(Point source, Point pivot, double angle)
    {
        var dx = source.X - pivot.X;
        var dy = source.Y - pivot.Y;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);

        return new Point(
            pivot.X + (dx * cos) - (dy * sin),
            pivot.Y + (dx * sin) + (dy * cos));
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2d;
        }
        while (angle < -Math.PI)
        {
            angle += Math.PI * 2d;
        }

        return angle;
    }

    private static double EaseInOutCubic(double value)
    {
        return value < 0.5d
            ? 4d * value * value * value
            : 1d - Math.Pow(-2d * value + 2d, 3d) / 2d;
    }

    private Point GetJointPoint(IReadOnlyList<Point> chainPoints, int jointIndex)
    {
        var pointIndex = _game.CurrentState.GetPointIndexForJoint(jointIndex);
        return chainPoints[pointIndex];
    }

    private static Color ParseColor(string hexColor, Color fallback)
    {
        return Color.TryParse(hexColor, out var color)
            ? color
            : fallback;
    }
}
