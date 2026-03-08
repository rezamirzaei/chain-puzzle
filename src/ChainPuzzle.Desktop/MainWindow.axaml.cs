using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ChainPuzzle.Core;
using ChainPuzzle.Desktop.Controls;
using ChainPuzzle.Desktop.ViewModels;
using System.Diagnostics;

namespace ChainPuzzle.Desktop;

/// <summary>
/// Main window for the Chain Chapters game.
/// Handles rendering, animation, drag input, and keyboard shortcuts.
/// All game logic is delegated to <see cref="GameViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly TimeSpan CelebrationDuration = TimeSpan.FromMilliseconds(1100);

    private sealed record RotationAnimation(
        ChainState FromState,
        ChainState ToState,
        int JointIndex,
        int Rotation,
        TimeSpan StartedAt,
        TimeSpan Duration);

    private sealed record DragState(int JointIndex, double LastAngle, Point LastPoint);

    private readonly GameViewModel _vm;
    private readonly AudioFeedbackService _audio = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _frameTimer;

    private readonly ChainBoardControl _board;
    private readonly Border _homeOverlay;
    private readonly Border _boardReadCard;
    private readonly Border _selectionCard;
    private readonly Border _statusCard;
    private readonly Border _validationBadge;
    private readonly Border _solvedCard;
    private readonly WrapPanel _homeChapterGallery;
    private readonly TextBlock _homeTitleText;
    private readonly TextBlock _homeSummaryText;
    private readonly TextBlock _homeProgressText;
    private readonly TextBlock _homeBestText;
    private readonly TextBlock _homeMedalText;
    private readonly ComboBox _animationSpeedPicker;
    private readonly CheckBox _hintHighlightsCheckBox;
    private readonly CheckBox _soundEnabledCheckBox;
    private readonly CheckBox _expertModeCheckBox;
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _progressText;
    private readonly TextBlock _completedText;
    private readonly TextBlock _movesText;
    private readonly TextBlock _bestText;
    private readonly TextBlock _difficultyText;
    private readonly TextBlock _modeText;
    private readonly TextBlock _boardReadText;
    private readonly TextBlock _selectionText;
    private readonly TextBlock _validationText;
    private readonly TextBlock _approachText;
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
    private TimeSpan? _celebrationStartedAt;
    private bool _isInitializing = true;
    private bool _isSyncingPicker;
    private bool _isSyncingSettings;

    public MainWindow()
    {
        InitializeComponent();

        _board = GetRequiredControl<ChainBoardControl>("Board");
        _homeOverlay = GetRequiredControl<Border>("HomeOverlay");
        _boardReadCard = GetRequiredControl<Border>("BoardReadCard");
        _selectionCard = GetRequiredControl<Border>("SelectionCard");
        _statusCard = GetRequiredControl<Border>("StatusCard");
        _validationBadge = GetRequiredControl<Border>("ValidationBadge");
        _solvedCard = GetRequiredControl<Border>("SolvedCard");
        _homeChapterGallery = GetRequiredControl<WrapPanel>("HomeChapterGallery");
        _homeTitleText = GetRequiredControl<TextBlock>("HomeTitleText");
        _homeSummaryText = GetRequiredControl<TextBlock>("HomeSummaryText");
        _homeProgressText = GetRequiredControl<TextBlock>("HomeProgressText");
        _homeBestText = GetRequiredControl<TextBlock>("HomeBestText");
        _homeMedalText = GetRequiredControl<TextBlock>("HomeMedalText");
        _animationSpeedPicker = GetRequiredControl<ComboBox>("AnimationSpeedPicker");
        _hintHighlightsCheckBox = GetRequiredControl<CheckBox>("HintHighlightsCheckBox");
        _soundEnabledCheckBox = GetRequiredControl<CheckBox>("SoundEnabledCheckBox");
        _expertModeCheckBox = GetRequiredControl<CheckBox>("ExpertModeCheckBox");
        _titleText = GetRequiredControl<TextBlock>("TitleText");
        _subtitleText = GetRequiredControl<TextBlock>("SubtitleText");
        _descriptionText = GetRequiredControl<TextBlock>("DescriptionText");
        _progressText = GetRequiredControl<TextBlock>("ProgressText");
        _completedText = GetRequiredControl<TextBlock>("CompletedText");
        _movesText = GetRequiredControl<TextBlock>("MovesText");
        _bestText = GetRequiredControl<TextBlock>("BestText");
        _difficultyText = GetRequiredControl<TextBlock>("DifficultyText");
        _modeText = GetRequiredControl<TextBlock>("ModeText");
        _boardReadText = GetRequiredControl<TextBlock>("BoardReadText");
        _selectionText = GetRequiredControl<TextBlock>("SelectionText");
        _validationText = GetRequiredControl<TextBlock>("ValidationText");
        _approachText = GetRequiredControl<TextBlock>("ApproachText");
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

        _vm = new GameViewModel();
        _vm.PropertyChanged += (_, _) => SyncViewFromViewModel();

        _frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _frameTimer.Tick += OnFrameTimerTick;
        Closed += (_, _) => _vm.SaveSettings();

        SyncViewFromViewModel();
        RenderFrame();
        _isInitializing = false;
    }

    // =====================
    //  View ↔ ViewModel sync
    // =====================

    private void SyncViewFromViewModel()
    {
        var accent = ParseColor(_vm.AccentHex, Colors.SteelBlue);

        _titleText.Text = _vm.Title;
        _subtitleText.Text = _vm.Subtitle;
        _descriptionText.Text = _vm.Description;
        _progressText.Text = _vm.ProgressText;
        _completedText.Text = _vm.CompletedText;
        _movesText.Text = _vm.MovesText;
        _bestText.Text = _vm.BestText;
        _difficultyText.Text = _vm.DifficultyText;
        _modeText.Text = _vm.ModeText;
        _boardReadText.Text = _vm.BoardReadText;
        _selectionText.Text = _vm.SelectionText;
        _validationText.Text = _vm.BadgeText;
        _approachText.Text = _vm.ApproachText;
        _statusText.Text = _vm.StatusText;

        _validationBadge.Background = new SolidColorBrush(ParseColor(_vm.BadgeBg, Colors.Transparent));
        _validationBadge.BorderBrush = new SolidColorBrush(ParseColor(_vm.BadgeBorder, Colors.Transparent));
        _validationText.Foreground = new SolidColorBrush(ParseColor(_vm.BadgeFg, accent));
        _selectionCard.Background = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B));
        _selectionCard.BorderBrush = new SolidColorBrush(Color.FromArgb(76, accent.R, accent.G, accent.B));
        _statusCard.Background = new SolidColorBrush(Color.FromArgb(22, accent.R, accent.G, accent.B));
        _statusCard.BorderBrush = new SolidColorBrush(Color.FromArgb(58, accent.R, accent.G, accent.B));
        _boardReadCard.BorderBrush = new SolidColorBrush(Color.FromArgb(34, accent.R, accent.G, accent.B));

        _homeOverlay.IsVisible = _vm.IsHomeVisible;
        _homeTitleText.Text = _vm.HomeTitle;
        _homeSummaryText.Text = _vm.HomeSummary;
        _homeProgressText.Text = _vm.HomeProgressInfo;
        _homeBestText.Text = _vm.HomeBestInfo;
        _homeMedalText.Text = _vm.HomeMedalInfo;
        _homePrimaryButton.Content = _vm.HomePrimaryLabel;
        _homeSecondaryButton.IsVisible = _vm.HomeSecondaryVisible;
        _homeCloseButton.IsVisible = _vm.HomeAllowsClose;

        _isSyncingSettings = true;
        _animationSpeedPicker.SelectedIndex = Math.Clamp(_vm.AnimationSpeed, 0, 2);
        _hintHighlightsCheckBox.IsChecked = _vm.ShowHintHighlights;
        _soundEnabledCheckBox.IsChecked = _vm.SoundEnabled;
        _expertModeCheckBox.IsChecked = _vm.ExpertMode;
        _isSyncingSettings = false;

        _solvedCard.IsVisible = _vm.ShowSolvedCard;
        _solvedTitleText.Text = _vm.SolvedTitle;
        _solvedSummaryText.Text = _vm.SolvedSummary;
        _solvedNextButton.IsEnabled = _vm.CanAdvance;
        _solvedNextButton.Content = _vm.CanAdvance ? "Next Chapter" : "All Cleared";

        var canInteract = _vm.CanInteract;
        _previousButton.IsEnabled = canInteract && _vm.LevelIndex > 0;
        _nextButton.IsEnabled = canInteract && _vm.LevelIndex < _vm.Levels.Count - 1;
        _undoButton.IsEnabled = canInteract && _vm.CanUndo;
        _redoButton.IsEnabled = canInteract && _vm.CanRedo;
        _menuButton.IsEnabled = !_vm.IsBusy;
        _resetButton.IsEnabled = canInteract;
        _hintButton.IsEnabled = canInteract && _vm.CanUseHint;
        _chapterPicker.IsEnabled = canInteract;
        _rotateLeftButton.IsEnabled = _vm.CanRotateManually;
        _rotateRightButton.IsEnabled = _vm.CanRotateManually;
        _hintButton.Content = _vm.ExpertMode ? "Nudge Locked" : "Nudge";

        var accentBrush = new SolidColorBrush(accent);
        _nextButton.Background = accentBrush;
        _hintButton.Background = accentBrush;
        _rotateLeftButton.Background = accentBrush;
        _rotateRightButton.Background = accentBrush;
        _solvedNextButton.Background = accentBrush;

        _isSyncingPicker = true;
        _chapterPicker.ItemsSource = _vm.ChapterPickerItems;
        _chapterPicker.SelectedIndex = _vm.ChapterPickerSelectedIndex;
        _isSyncingPicker = false;

        SyncHomeChapterGallery();
        RenderFrame();
    }

    private void SyncHomeChapterGallery()
    {
        _homeChapterGallery.Children.Clear();
        foreach (var card in _vm.ChapterCards)
        {
            _homeChapterGallery.Children.Add(BuildChapterCard(card));
        }
    }

    private Button BuildChapterCard(ChapterGalleryCard card)
    {
        var accent = ParseColor(card.AccentHex, Colors.SteelBlue);
        var background = new SolidColorBrush(card.IsCurrent
            ? Color.FromArgb(24, accent.R, accent.G, accent.B)
            : Colors.White);
        var borderBrush = new SolidColorBrush(card.IsCurrent
            ? accent
            : ParseColor("#D1D5DB", Colors.LightGray));
        var mutedBrush = new SolidColorBrush(ParseColor("#64748B", Colors.Gray));
        var titleBrush = new SolidColorBrush(ParseColor("#0F172A", Colors.Black));
        var accentBrush = new SolidColorBrush(accent);
        var (medalBackground, medalBorder, medalForeground) = GetMedalBrushes(card.MedalLabel);

        var preview = new ShapePreviewControl
        {
            Height = 92,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        preview.UpdatePreview(card.TargetPoints, card.AccentHex, card.IsCurrent);

        var difficultyChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(8, 3),
            Child = new TextBlock
            {
                Text = card.DifficultyText,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = accentBrush
            }
        };

        var methodChip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(8, 3),
            Child = new TextBlock
            {
                Text = card.MethodText,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = mutedBrush
            }
        };

        var chipRow = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                difficultyChip,
                methodChip
            }
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };

        var chapterText = new TextBlock
        {
            Text = $"Chapter {card.NumberText}",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = accentBrush
        };
        header.Children.Add(chapterText);

        var medalChip = new Border
        {
            Background = medalBackground,
            BorderBrush = medalBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 3),
            Child = new TextBlock
            {
                Text = card.MedalLabel,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = medalForeground
            }
        };
        Grid.SetColumn(medalChip, 1);
        header.Children.Add(medalChip);

        var cardContent = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                header,
                new TextBlock
                {
                    Text = card.Subtitle,
                    FontSize = 18,
                    FontFamily = "Georgia",
                    FontWeight = FontWeight.SemiBold,
                    Foreground = titleBrush
                },
                chipRow,
                new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(12, accent.R, accent.G, accent.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(6),
                    Child = preview
                },
                new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 8),
                    Child = new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Best Line",
                                FontSize = 10,
                                FontWeight = FontWeight.Bold,
                                Foreground = mutedBrush
                            },
                            new TextBlock
                            {
                                Text = card.BestText,
                                FontSize = 13,
                                FontWeight = FontWeight.Bold,
                                Foreground = titleBrush
                            }
                        }
                    }
                },
                new TextBlock
                {
                    Text = card.PressureText,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = mutedBrush
                },
                new TextBlock
                {
                    Text = card.BranchText,
                    FontSize = 11,
                    Foreground = mutedBrush,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        var button = new Button
        {
            Classes = { "chapterCard" },
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(card.IsCurrent ? 2 : 1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = cardContent
        };

        button.Click += (_, _) =>
        {
            if (_vm.IsBusy)
            {
                return;
            }

            ClearTransientVisualState();
            _vm.OpenChapterFromHome(card.Index);
        };

        return button;
    }

    private static (IBrush Background, IBrush Border, IBrush Foreground) GetMedalBrushes(string medalLabel)
    {
        return medalLabel switch
        {
            "Gold" => Brushes(
                Color.FromArgb(255, 254, 243, 199),
                Color.FromArgb(255, 245, 158, 11),
                Color.FromArgb(255, 146, 64, 14)),
            "Silver" => Brushes(
                Color.FromArgb(255, 226, 232, 240),
                Color.FromArgb(255, 148, 163, 184),
                Color.FromArgb(255, 51, 65, 85)),
            "Bronze" => Brushes(
                Color.FromArgb(255, 245, 224, 211),
                Color.FromArgb(255, 192, 132, 87),
                Color.FromArgb(255, 124, 45, 18)),
            _ => Brushes(
                Color.FromArgb(255, 248, 250, 252),
                Color.FromArgb(255, 203, 213, 225),
                Color.FromArgb(255, 71, 85, 105))
        };

        static (IBrush Background, IBrush Border, IBrush Foreground) Brushes(Color background, Color border, Color foreground)
        {
            return (new SolidColorBrush(background), new SolidColorBrush(border), new SolidColorBrush(foreground));
        }
    }

    // =====================
    //  Rendering
    // =====================

    private void RenderFrame()
    {
        UpdateAnimationState();

        var chainPoints = GetDisplayPoints();
        var jointPoints = GetDisplayJointPoints(chainPoints);
        var segmentSpans = _vm.CurrentState.GetSegmentSpans();
        _board.UpdateScene(
            chainPoints,
            jointPoints,
            segmentSpans,
            _vm.CurrentState.GetPoints(),
            _vm.CurrentLevel.TargetPoints,
            _vm.SelectedJointIndex,
            _vm.AccentHex,
            _vm.IsSolved && !_vm.IsBusy,
            GetCelebrationProgress());
    }

    private void OnFrameTimerTick(object? sender, EventArgs e)
    {
        if (_animation is null && _celebrationStartedAt is null) { _frameTimer.Stop(); return; }
        RenderFrame();
    }

    private void UpdateAnimationState()
    {
        if (_animation is null) return;
        var elapsed = _clock.Elapsed - _animation.StartedAt;
        if (elapsed < _animation.Duration) return;
        _animation = null;
        var solvedNow = _vm.IsSolved;
        _vm.OnAnimationCompleted();
        if (solvedNow)
        {
            StartCelebration();
            _audio.Play(AudioCue.ChapterSolved, _vm.SoundEnabled);
        }
    }

    private IReadOnlyList<Point> GetDisplayPoints()
    {
        if (_animation is null)
        {
            return _vm.CurrentState.GetPoints()
                .Select(p => new Point(p.X, p.Y)).ToArray();
        }

        var elapsed = _clock.Elapsed - _animation.StartedAt;
        var progress = Math.Clamp(elapsed.TotalMilliseconds / _animation.Duration.TotalMilliseconds, 0d, 1d);
        var eased = EaseInOutCubic(progress);
        var angle = _animation.Rotation * (Math.PI / 3d) * eased;

        var source = _animation.FromState.GetPoints();
        var pivotIdx = _animation.FromState.GetPointIndexForJoint(_animation.JointIndex);
        var pivot = new Point(source[pivotIdx].X, source[pivotIdx].Y);

        var animated = new Point[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var s = new Point(source[i].X, source[i].Y);
            animated[i] = i < pivotIdx ? s : RotateAround(s, pivot, angle);
        }
        return animated;
    }

    private IReadOnlyList<Point> GetDisplayJointPoints(IReadOnlyList<Point> chainPoints)
    {
        var indices = _vm.CurrentState.GetJointPointIndices();
        var pts = new Point[indices.Count];
        for (var i = 0; i < indices.Count; i++) pts[i] = chainPoints[indices[i]];
        return pts;
    }

    // =====================
    //  Move execution
    // =====================

    private bool TryRotate(int jointIndex, int rotation)
    {
        if (_vm.IsBusy || _vm.IsSolved) return false;

        var fromState = _vm.CurrentState;
        if (!_vm.TryRotate(jointIndex, rotation))
        {
            _audio.Play(AudioCue.BlockedMove, _vm.SoundEnabled);
            return false;
        }

        _vm.IsAnimating = true;
        _animation = new RotationAnimation(
            fromState, _vm.CurrentState, jointIndex, rotation,
            _clock.Elapsed, TimeSpan.FromMilliseconds(_vm.AnimationDurationMs));
        EnsureFrameLoop();
        return true;
    }

    private double GetCelebrationProgress()
    {
        if (_celebrationStartedAt is not { } startedAt)
        {
            return 0d;
        }

        var elapsed = _clock.Elapsed - startedAt;
        if (elapsed >= CelebrationDuration)
        {
            _celebrationStartedAt = null;
            return 0d;
        }

        return Math.Clamp(elapsed.TotalMilliseconds / CelebrationDuration.TotalMilliseconds, 0d, 1d);
    }

    private void StartCelebration()
    {
        _celebrationStartedAt = _clock.Elapsed;
        EnsureFrameLoop();
    }

    private void ClearTransientVisualState()
    {
        _animation = null;
        _dragState = null;
        _celebrationStartedAt = null;
    }

    private void EnsureFrameLoop()
    {
        if (!_frameTimer.IsEnabled) _frameTimer.Start();
    }

    // =====================
    //  Event handlers
    // =====================

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private T GetRequiredControl<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Required control \"{name}\" not found.");

    private void HomePrimaryButton_OnClick(object? sender, RoutedEventArgs e) => _vm.HomePrimaryCommand.Execute(null);
    private void HomeSecondaryButton_OnClick(object? sender, RoutedEventArgs e) => _vm.HomeSecondaryCommand.Execute(null);
    private void HomeCloseButton_OnClick(object? sender, RoutedEventArgs e) => _vm.HomeCloseCommand.Execute(null);

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ClearTransientVisualState();
        _vm.PreviousLevelCommand.Execute(null);
    }

    private void NextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ClearTransientVisualState();
        _vm.NextLevelCommand.Execute(null);
    }

    private void UndoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;
        ClearTransientVisualState();
        _vm.UndoCommand.Execute(null);
    }

    private void RedoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;
        ClearTransientVisualState();
        _vm.RedoCommand.Execute(null);
    }

    private void MenuButton_OnClick(object? sender, RoutedEventArgs e) => _vm.GoHomeCommand.Execute(null);

    private void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ClearTransientVisualState();
        _vm.ResetLevelCommand.Execute(null);
    }

    private async void HintButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _vm.FindHintCommand.ExecuteAsync(null);
        RenderFrame();
    }

    private void RotateLeftButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedJointIndex is null) { _vm.RotateLeftCommand.Execute(null); return; }
        TryRotate(_vm.SelectedJointIndex.Value, -1);
    }

    private void RotateRightButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedJointIndex is null) { _vm.RotateRightCommand.Execute(null); return; }
        TryRotate(_vm.SelectedJointIndex.Value, 1);
    }

    private void SolvedResetButton_OnClick(object? sender, RoutedEventArgs e) => ResetButton_OnClick(sender, e);

    private void SolvedNextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.LevelIndex >= _vm.Levels.Count - 1) return;
        NextButton_OnClick(sender, e);
    }

    private void ChapterPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _vm is null || sender is not ComboBox picker)
        {
            return;
        }

        if (_isSyncingPicker || picker.SelectedIndex < 0 || picker.SelectedIndex == _vm.LevelIndex) return;
        ClearTransientVisualState();
        _vm.SelectChapter(picker.SelectedIndex);
    }

    private void AnimationSpeedPicker_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || _vm is null || sender is not ComboBox picker || _isSyncingSettings || picker.SelectedIndex < 0)
        {
            return;
        }

        _vm.AnimationSpeed = Math.Clamp(picker.SelectedIndex, 0, 2);
    }

    private void SettingsCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing || _vm is null || _isSyncingSettings)
        {
            return;
        }

        _vm.ShowHintHighlights = _hintHighlightsCheckBox.IsChecked ?? true;
        _vm.SoundEnabled = _soundEnabledCheckBox.IsChecked ?? false;
        _vm.ExpertMode = _expertModeCheckBox.IsChecked ?? false;
    }

    // =====================
    //  Pointer / keyboard
    // =====================

    private void Board_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm.IsBusy || _vm.IsHomeVisible || _vm.IsSolved) return;
        if (!e.GetCurrentPoint(_board).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(_board);
        var joint = _board.HitTestJoint(pos);
        if (joint is null) { _vm.SelectJoint(null); return; }

        _vm.SelectJoint(joint);
        var chainPts = GetDisplayPoints();
        var pivotPt = chainPts[_vm.CurrentState.GetPointIndexForJoint(joint.Value)];
        var pivot = _board.WorldToScreen(pivotPt);
        _dragState = new DragState(joint.Value, Math.Atan2(pos.Y - pivot.Y, pos.X - pivot.X), pos);
        e.Pointer.Capture(_board);
    }

    private void Board_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragState is null || _vm.IsBusy || _vm.IsHomeVisible) return;

        var pos = e.GetPosition(_board);
        var chainPts = GetDisplayPoints();
        var pivotPt = chainPts[_vm.CurrentState.GetPointIndexForJoint(_dragState.JointIndex)];
        var pivot = _board.WorldToScreen(pivotPt);
        var angle = Math.Atan2(pos.Y - pivot.Y, pos.X - pivot.X);
        var delta = NormalizeAngle(angle - _dragState.LastAngle);
        var hDelta = pos.X - _dragState.LastPoint.X;

        if (Math.Abs(delta) < Math.PI / 8d && Math.Abs(hDelta) < 12d) return;

        var rotation = Math.Abs(hDelta) > Math.Abs(delta * 48d) ? (hDelta > 0 ? 1 : -1) : (delta > 0 ? 1 : -1);
        if (TryRotate(_dragState.JointIndex, rotation))
            _dragState = _dragState with { LastAngle = angle, LastPoint = pos };
    }

    private void Board_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragState = null;
        e.Pointer.Capture(null);
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm.IsHomeVisible)
        {
            if (e.Key == Key.Escape && _vm.HomeAllowsClose) { _vm.HomeCloseCommand.Execute(null); e.Handled = true; return; }
            if (e.Key is Key.Enter or Key.Space) { _vm.HomePrimaryCommand.Execute(null); e.Handled = true; return; }
            if (e.Key == Key.N && _vm.HomeSecondaryVisible) { _vm.HomeSecondaryCommand.Execute(null); e.Handled = true; return; }
            return;
        }

        if (e.Key == Key.Escape) { _vm.SelectJoint(null); return; }
        if (_vm.IsBusy) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var wantsRedo = e.Key == Key.Y || (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            if (e.Key == Key.Z && !wantsRedo) { UndoButton_OnClick(sender, new RoutedEventArgs()); e.Handled = true; return; }
            if (wantsRedo) { RedoButton_OnClick(sender, new RoutedEventArgs()); e.Handled = true; return; }
        }

        if (_vm.IsSolved) return;

        if (e.Key is Key.Up or Key.Down)
        {
            var min = 1;
            var max = _vm.CurrentState.SegmentCount - 1;
            if (max < min) return;
            if (_vm.SelectedJointIndex is null) { _vm.SelectJoint(min); }
            else
            {
                var next = _vm.SelectedJointIndex.Value + (e.Key == Key.Up ? -1 : 1);
                if (next < min) next = max;
                if (next > max) next = min;
                _vm.SelectJoint(next);
            }
            e.Handled = true;
            return;
        }

        if (_vm.SelectedJointIndex is not null && e.Key is Key.A or Key.D)
        {
            TryRotate(_vm.SelectedJointIndex.Value, e.Key == Key.A ? -1 : 1);
            e.Handled = true;
        }
    }

    // =====================
    //  Math helpers
    // =====================

    private static Point RotateAround(Point s, Point p, double a)
    {
        var dx = s.X - p.X; var dy = s.Y - p.Y;
        return new Point(p.X + dx * Math.Cos(a) - dy * Math.Sin(a), p.Y + dx * Math.Sin(a) + dy * Math.Cos(a));
    }

    private static double NormalizeAngle(double a)
    {
        while (a > Math.PI) a -= Math.PI * 2d;
        while (a < -Math.PI) a += Math.PI * 2d;
        return a;
    }

    private static double EaseInOutCubic(double v) =>
        v < 0.5d ? 4d * v * v * v : 1d - Math.Pow(-2d * v + 2d, 3d) / 2d;

    private static Color ParseColor(string hex, Color fallback) =>
        Color.TryParse(hex, out var c) ? c : fallback;
}
