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
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _frameTimer;

    private readonly ChainBoardControl _board;
    private readonly TextBlock _titleText;
    private readonly TextBlock _subtitleText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _progressText;
    private readonly TextBlock _movesText;
    private readonly TextBlock _optimalText;
    private readonly TextBlock _validationText;
    private readonly TextBlock _statusText;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _resetButton;
    private readonly Button _hintButton;
    private readonly Button _rotateLeftButton;
    private readonly Button _rotateRightButton;

    private RotationAnimation? _animation;
    private DragState? _dragState;
    private int? _selectedJointIndex;
    private bool _isFindingHint;
    private string _statusMessage = "Drag a joint to rotate the chain and fit the target shape.";

    public MainWindow()
    {
        InitializeComponent();

        _board = GetRequiredControl<ChainBoardControl>("Board");
        _titleText = GetRequiredControl<TextBlock>("TitleText");
        _subtitleText = GetRequiredControl<TextBlock>("SubtitleText");
        _descriptionText = GetRequiredControl<TextBlock>("DescriptionText");
        _progressText = GetRequiredControl<TextBlock>("ProgressText");
        _movesText = GetRequiredControl<TextBlock>("MovesText");
        _optimalText = GetRequiredControl<TextBlock>("OptimalText");
        _validationText = GetRequiredControl<TextBlock>("ValidationText");
        _statusText = GetRequiredControl<TextBlock>("StatusText");
        _previousButton = GetRequiredControl<Button>("PreviousButton");
        _nextButton = GetRequiredControl<Button>("NextButton");
        _resetButton = GetRequiredControl<Button>("ResetButton");
        _hintButton = GetRequiredControl<Button>("HintButton");
        _rotateLeftButton = GetRequiredControl<Button>("RotateLeftButton");
        _rotateRightButton = GetRequiredControl<Button>("RotateRightButton");

        _game = new ChapterGame(ChapterFactory.CreateChapters());
        _frameTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _frameTimer.Tick += OnFrameTimerTick;

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
            _statusMessage = $"{_game.CurrentLevel.Subtitle} solved. Continue to the next chapter.";
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
        var validation = level.Validation;

        _titleText.Text = level.Title;
        _subtitleText.Text = level.Subtitle;
        _descriptionText.Text = level.Description;

        _progressText.Text = $"{_game.LevelIndex + 1}/{_game.Levels.Count}";
        _movesText.Text = _game.Moves.ToString(CultureInfo.InvariantCulture);
        _optimalText.Text = validation?.ShortestPathLength.ToString(CultureInfo.InvariantCulture) ?? "-";

        _validationText.Text = validation is null
            ? "Validation pending"
            : validation.SolutionCount == 1
                ? "Unique solution verified"
                : "Invalid chapter";

        var solvedStatus = _game.IsSolved && !IsBusy
            ? "Solved. Use Next to continue."
            : _statusMessage;
        _statusText.Text = _selectedJointIndex is null
            ? solvedStatus
            : $"{solvedStatus} Selected joint: {_selectedJointIndex}.";

        _previousButton.IsEnabled = _game.LevelIndex > 0;
        _nextButton.IsEnabled = _game.LevelIndex < _game.Levels.Count - 1;
        _resetButton.IsEnabled = !IsBusy;
        _hintButton.IsEnabled = !IsBusy && !_game.IsSolved;
        var canRotateManually = !IsBusy && !_game.IsSolved && _selectedJointIndex is not null;
        _rotateLeftButton.IsEnabled = canRotateManually;
        _rotateRightButton.IsEnabled = canRotateManually;

        var accent = new SolidColorBrush(ParseColor(level.AccentHex, Colors.SteelBlue));
        _previousButton.Background = accent;
        _nextButton.Background = accent;
        _resetButton.Background = accent;
        _hintButton.Background = accent;
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

    private void ChangeLevel(int nextIndex, string status)
    {
        _game.SetLevel(nextIndex);
        _animation = null;
        _isFindingHint = false;
        _dragState = null;
        _selectedJointIndex = null;
        _statusMessage = status;
        RefreshHud();
        RenderFrame();
    }

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeLevel(_game.LevelIndex - 1, $"Chapter {_game.LevelIndex} loaded.");
    }

    private void NextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeLevel(_game.LevelIndex + 1, $"Chapter {_game.LevelIndex + 2} loaded.");
    }

    private void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _game.ResetLevel();
        _animation = null;
        _isFindingHint = false;
        _dragState = null;
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
        _statusMessage = "Computing hint...";
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
            _statusMessage = "No hint available right now.";
            RefreshHud();
            return;
        }

        var applied = TryRotate(hint.Value.JointIndex, hint.Value.Rotation);
        _statusMessage = applied
            ? "Hint applied: one optimal move executed."
            : "Hint move blocked.";
        RefreshHud();
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

    private void Board_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsBusy)
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
        if (_dragState is null || IsBusy)
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
