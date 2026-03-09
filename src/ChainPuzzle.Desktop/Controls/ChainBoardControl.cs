using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChainPuzzle.Core;

namespace ChainPuzzle.Desktop.Controls;

/// <summary>
/// Custom Avalonia control that renders the hex-grid game board:
/// target silhouette, chain segments, and joint indicators.
/// Caches brushes and pens to minimize allocation during render.
/// </summary>
public sealed class ChainBoardControl : Control
{
    private const double HexHeight = 0.8660254037844386; // sqrt(3) / 2

    public static readonly StyledProperty<Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.Register<ChainBoardControl, Thickness>(
            nameof(SafeAreaPadding),
            new Thickness(140));

    private readonly List<Point> _chainPoints = new();
    private readonly HashSet<IntPoint> _chainCoverage = new();
    private readonly List<Point> _jointPoints = new();
    private readonly List<SegmentSpan> _segmentSpans = new();
    private readonly List<IntPoint> _targetPoints = new();

    private int? _activeJointIndex;
    private Color _accentColor = ParseColor("#f48c06", Colors.Orange);
    private bool _isSolved;
    private double _celebrationProgress;

    private double _scale = 1d;
    private double _offsetX;
    private double _offsetY;

    // Cached brushes/pens — recreated only when accent or solved state changes.
    private SolidColorBrush? _targetGlowBrush;
    private SolidColorBrush? _targetTileBrush;
    private SolidColorBrush? _targetDimTileBrush;
    private Pen? _targetTilePen;
    private Pen? _targetDimTilePen;
    private SolidColorBrush? _targetHighlightBrush;
    private Color _cachedAccentForBrushes;
    private bool _cachedSolvedForBrushes;

    // Cached background brushes — static, never change.
    private LinearGradientBrush? _bgGradient;
    private SolidColorBrush? _bgGlow;
    private SolidColorBrush? _bgSecondaryGlow;
    private SolidColorBrush? _bgCenterGlow;
    private Pen? _bgLatticePen;
    private Pen? _bgAccentRingPen;
    private Color _cachedAccentForBg;

    // Cached chain brushes/pens — recreated only when accent changes.
    private SolidColorBrush? _chainLightWood;
    private SolidColorBrush? _chainDarkWood;
    private SolidColorBrush? _chainShadow;
    private SolidColorBrush? _chainEdgeBrush;
    private SolidColorBrush? _chainMetalBrush;
    private Color _cachedAccentForChain;

    public ChainBoardControl()
    {
        ClipToBounds = true;
    }

    public Thickness SafeAreaPadding
    {
        get => GetValue(SafeAreaPaddingProperty);
        set => SetValue(SafeAreaPaddingProperty, value);
    }

    public void UpdateScene(
        IReadOnlyList<Point> chainPoints,
        IReadOnlyList<Point> jointPoints,
        IReadOnlyList<SegmentSpan> segmentSpans,
        IReadOnlyList<IntPoint> chainCoveragePoints,
        IReadOnlyList<IntPoint> targetPoints,
        int? activeJointIndex,
        string accentHex,
        bool isSolved,
        double celebrationProgress = 0d)
    {
        _chainPoints.Clear();
        _chainPoints.AddRange(chainPoints);

        _jointPoints.Clear();
        _jointPoints.AddRange(jointPoints);

        _chainCoverage.Clear();
        foreach (var point in chainCoveragePoints)
        {
            _chainCoverage.Add(point);
        }

        _segmentSpans.Clear();
        _segmentSpans.AddRange(segmentSpans);

        var targetChanged = _targetPoints.Count != targetPoints.Count;
        if (!targetChanged)
        {
            for (var index = 0; index < targetPoints.Count; index += 1)
            {
                if (_targetPoints[index] != targetPoints[index])
                {
                    targetChanged = true;
                    break;
                }
            }
        }

        if (targetChanged)
        {
            _targetPoints.Clear();
            foreach (var targetPoint in targetPoints)
            {
                _targetPoints.Add(targetPoint);
            }

        }

        _activeJointIndex = activeJointIndex;
        _isSolved = isSolved;
        _celebrationProgress = Math.Clamp(celebrationProgress, 0d, 1d);
        _accentColor = ParseColor(accentHex, _accentColor);

        RecalculateTransform();
        InvalidateVisual();
    }

    public Point WorldToScreen(Point worldPoint)
    {
        var projected = Project(worldPoint);
        return new Point(
            projected.X * _scale + _offsetX,
            projected.Y * _scale + _offsetY);
    }

    public Point WorldToScreen(IntPoint worldPoint)
    {
        return WorldToScreen(new Point(worldPoint.X, worldPoint.Y));
    }

    public int? HitTestJoint(Point screenPoint, double maxDistance = 56d)
    {
        if (_jointPoints.Count == 0)
        {
            return null;
        }

        var effectiveMaxDistance = maxDistance <= 0d
            ? Math.Max(22d, _scale * 0.8d)
            : Math.Max(12d, maxDistance);

        var bestDistance = double.MaxValue;
        int? bestIndex = null;

        for (var index = 0; index < _jointPoints.Count; index += 1)
        {
            var joint = WorldToScreen(_jointPoints[index]);
            var dx = joint.X - screenPoint.X;
            var dy = joint.Y - screenPoint.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= effectiveMaxDistance && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index + 1;
            }
        }

        return bestIndex;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        RecalculateTransform();
        DrawBackground(context);
        DrawTarget(context);
        DrawChain(context);
        DrawCelebration(context);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SafeAreaPaddingProperty)
        {
            RecalculateTransform();
            InvalidateVisual();
        }
    }

    private void RecalculateTransform()
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            _scale = 1d;
            _offsetX = Bounds.Width / 2d;
            _offsetY = Bounds.Height / 2d;
            return;
        }

        var hasReferencePoints = _targetPoints.Count > 0 || _chainPoints.Count > 0;
        if (!hasReferencePoints)
        {
            _scale = 1d;
            _offsetX = Bounds.Width / 2d;
            _offsetY = Bounds.Height / 2d;
            return;
        }

        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        if (_targetPoints.Count > 0)
        {
            foreach (var point in _targetPoints)
            {
                IncludePoint(Project(new Point(point.X, point.Y)), ref minX, ref maxX, ref minY, ref maxY);
            }
        }

        if (_chainPoints.Count > 0)
        {
            foreach (var point in _chainPoints)
            {
                IncludePoint(Project(point), ref minX, ref maxX, ref minY, ref maxY);
            }
        }

        var width = Math.Max(1d, maxX - minX);
        var height = Math.Max(1d, maxY - minY);

        var safePadding = SafeAreaPadding;
        var safeLeft = Math.Clamp(safePadding.Left, 0d, Math.Max(0d, Bounds.Width - 220d));
        var safeRight = Math.Clamp(safePadding.Right, 0d, Math.Max(0d, Bounds.Width - safeLeft - 220d));
        var safeTop = Math.Clamp(safePadding.Top, 0d, Math.Max(0d, Bounds.Height - 220d));
        var safeBottom = Math.Clamp(safePadding.Bottom, 0d, Math.Max(0d, Bounds.Height - safeTop - 220d));

        var safeWidth = Math.Max(220d, Bounds.Width - safeLeft - safeRight);
        var safeHeight = Math.Max(220d, Bounds.Height - safeTop - safeBottom);
        var innerHorizontalPadding = Math.Clamp(safeWidth * 0.08, 40d, 84d);
        var innerVerticalPadding = Math.Clamp(safeHeight * 0.10, 48d, 96d);
        var scaleX = (safeWidth - innerHorizontalPadding) / width;
        var scaleY = (safeHeight - innerVerticalPadding) / height;
        _scale = Math.Max(18d, Math.Min(scaleX, scaleY));

        var safeCenterX = safeLeft + (safeWidth / 2d);
        var safeCenterY = safeTop + (safeHeight / 2d);
        _offsetX = safeCenterX - ((minX + maxX) / 2d) * _scale;
        _offsetY = safeCenterY - ((minY + maxY) / 2d) * _scale;
    }

    private void DrawBackground(DrawingContext context)
    {
        EnsureBackgroundBrushes();

        context.DrawRectangle(_bgGradient, null, new Rect(0, 0, Bounds.Width, Bounds.Height));
        context.DrawEllipse(_bgGlow, null, new Point(Bounds.Width * 0.2, Bounds.Height * 0.2), Bounds.Width * 0.2, Bounds.Width * 0.2);
        context.DrawEllipse(_bgSecondaryGlow, null, new Point(Bounds.Width * 0.82, Bounds.Height * 0.78), Bounds.Width * 0.17, Bounds.Width * 0.17);
        context.DrawEllipse(_bgCenterGlow, null, new Point(Bounds.Width * 0.5, Bounds.Height * 0.52), Bounds.Width * 0.24, Bounds.Height * 0.19);
        DrawBackdropLattice(context);
        context.DrawEllipse(null, _bgAccentRingPen, new Point(Bounds.Width * 0.5, Bounds.Height * 0.52), Bounds.Width * 0.28, Bounds.Height * 0.22);
    }

    private void EnsureBackgroundBrushes()
    {
        if (_bgGradient is not null && _cachedAccentForBg == _accentColor)
        {
            return;
        }

        _cachedAccentForBg = _accentColor;
        var paper = ParseColor("#FFF8EF", Colors.White);
        var mist = ParseColor("#EAF4FF", Colors.White);
        var moss = ParseColor("#F3F8EC", Colors.White);
        var warmAccent = Mix(_accentColor, ParseColor("#F59E0B", Colors.Gold), 0.24);

        _bgGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Mix(paper, _accentColor, 0.08), 0),
                new GradientStop(Mix(mist, _accentColor, 0.05), 0.42),
                new GradientStop(Mix(moss, warmAccent, 0.08), 1)
            }
        };

        _bgGlow = new SolidColorBrush(Color.FromArgb(34, _accentColor.R, _accentColor.G, _accentColor.B));
        _bgSecondaryGlow = new SolidColorBrush(Color.FromArgb(28, warmAccent.R, warmAccent.G, warmAccent.B));
        _bgCenterGlow = new SolidColorBrush(Color.FromArgb(24, _accentColor.R, _accentColor.G, _accentColor.B));
        _bgLatticePen = new Pen(
            new SolidColorBrush(Color.FromArgb(20, _accentColor.R, _accentColor.G, _accentColor.B)),
            1.1);
        _bgAccentRingPen = new Pen(
            new SolidColorBrush(Color.FromArgb(18, _accentColor.R, _accentColor.G, _accentColor.B)),
            2);
    }

    private void DrawBackdropLattice(DrawingContext context)
    {
        if (_bgLatticePen is null)
        {
            return;
        }

        var rows = (int)Math.Ceiling(Bounds.Height / 82d) + 1;
        var cols = (int)Math.Ceiling(Bounds.Width / 96d) + 1;

        for (var row = 0; row < rows; row += 1)
        {
            for (var col = 0; col < cols; col += 1)
            {
                var center = new Point(
                    36 + (col * 96d) + ((row % 2) * 48d),
                    28 + (row * 82d));
                var radius = 12d + (((row + col) % 3) * 3d);

                context.DrawGeometry(null, _bgLatticePen, BuildHexTileGeometry(center, radius));
            }
        }
    }

    private void DrawTarget(DrawingContext context)
    {
        if (_targetPoints.Count == 0)
        {
            return;
        }

        EnsureTargetBrushes();
        var pulse = 1d + (_celebrationProgress > 0d
            ? Math.Sin(_celebrationProgress * Math.PI * 6d) * (1d - _celebrationProgress) * 0.09d
            : 0d);
        var glowRadius = _scale * (0.64 + (_celebrationProgress * 0.08)) * pulse;
        var tileRadius = _scale * 0.585 * pulse;
        var highlightRadius = _scale * (0.36 + (_celebrationProgress * 0.02));

        foreach (var point in _targetPoints)
        {
            var center = WorldToScreen(point);
            var isCovered = _chainCoverage.Contains(point);
            var tileBrush = isCovered ? _targetTileBrush : _targetDimTileBrush;
            var tilePen = isCovered ? _targetTilePen : _targetDimTilePen;

            context.DrawGeometry(_targetGlowBrush, null, BuildHexTileGeometry(center, glowRadius));
            context.DrawGeometry(tileBrush, tilePen, BuildHexTileGeometry(center, tileRadius));
            context.DrawGeometry(_targetHighlightBrush, null, BuildHexTileGeometry(center, highlightRadius));
        }
    }

    private void EnsureTargetBrushes()
    {
        if (_targetGlowBrush is not null
            && _cachedAccentForBrushes == _accentColor
            && _cachedSolvedForBrushes == _isSolved)
        {
            return;
        }

        _cachedAccentForBrushes = _accentColor;
        _cachedSolvedForBrushes = _isSolved;

        _targetGlowBrush = new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)72 : (byte)48, _accentColor.R, _accentColor.G, _accentColor.B));
        _targetTileBrush = new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)220 : (byte)185, _accentColor.R, _accentColor.G, _accentColor.B));
        _targetDimTileBrush = new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)125 : (byte)95, _accentColor.R, _accentColor.G, _accentColor.B));
        _targetTilePen = new Pen(
            new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)235 : (byte)205, _accentColor.R, _accentColor.G, _accentColor.B)),
            1.3);
        _targetDimTilePen = new Pen(
            new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)155 : (byte)125, 255, 255, 255)),
            1.05);
        _targetHighlightBrush = new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)70 : (byte)42, 255, 255, 255));
    }

    private void DrawChain(DrawingContext context)
    {
        if (_chainPoints.Count < 2 || _segmentSpans.Count == 0)
        {
            return;
        }

        EnsureChainBrushes();
        var celebrationGlowPen = _celebrationProgress > 0d
            ? new Pen(
                new SolidColorBrush(Color.FromArgb(
                    (byte)(50 + (_celebrationProgress * 70)),
                    _accentColor.R,
                    _accentColor.G,
                    _accentColor.B)),
                34 + (_celebrationProgress * 6),
                lineCap: PenLineCap.Round,
                lineJoin: PenLineJoin.Round)
            : null;

        for (var index = 0; index < _segmentSpans.Count; index += 1)
        {
            var span = _segmentSpans[index];
            var start = WorldToScreen(_chainPoints[span.StartIndex]);
            var end = WorldToScreen(_chainPoints[span.EndIndex]);
            var isLight = index % 2 == 0;
            var baseColor = isLight ? _chainLightWood! : _chainDarkWood!;

            var thickness = 20 + (span.Length - 1) * 3;
            var shadowPen = new Pen(_chainShadow, thickness + 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var segmentPen = new Pen(baseColor, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var edgePen = new Pen(_chainEdgeBrush, 2, lineCap: PenLineCap.Round);

            if (celebrationGlowPen is not null)
            {
                context.DrawLine(celebrationGlowPen, start, end);
            }

            context.DrawLine(shadowPen, start, end);
            context.DrawLine(segmentPen, start, end);
            context.DrawLine(edgePen, start, end);
        }

        DrawChainJoints(context);
    }

    private void EnsureChainBrushes()
    {
        if (_chainLightWood is not null && _cachedAccentForChain == _accentColor) return;
        _cachedAccentForChain = _accentColor;
        _chainLightWood = new SolidColorBrush(ParseColor("#F4EBD0", Colors.WhiteSmoke));
        _chainDarkWood = new SolidColorBrush(ParseColor("#B07D56", Colors.SaddleBrown));
        _chainShadow = new SolidColorBrush(Color.FromArgb(60, 20, 20, 20));
        _chainEdgeBrush = new SolidColorBrush(Color.FromArgb(180, 70, 45, 35));
        _chainMetalBrush = new SolidColorBrush(Mix(ParseColor("#A8A29E", Colors.SlateGray), _accentColor, 0.14));
    }

    private void DrawChainJoints(DrawingContext context)
    {
        if (_chainPoints.Count == 0)
        {
            return;
        }

        var root = WorldToScreen(_chainPoints[0]);
        var tip = WorldToScreen(_chainPoints[^1]);

        context.DrawEllipse(_chainMetalBrush, new Pen(_chainMetalBrush, 2), root, 9, 9);
        context.DrawEllipse(_chainMetalBrush, new Pen(_chainMetalBrush, 2), tip, 7, 7);

        for (var index = 0; index < _jointPoints.Count; index += 1)
        {
            var joint = WorldToScreen(_jointPoints[index]);
            var jointIndex = index + 1;
            var isActive = _activeJointIndex == jointIndex;

            var strokeBrush = isActive
                ? new SolidColorBrush(ParseColor("#D00000", Colors.Red))
                : new SolidColorBrush(ParseColor("#264653", Colors.DarkSlateGray));

            if (isActive)
            {
                context.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(44, _accentColor.R, _accentColor.G, _accentColor.B)),
                    null,
                    joint,
                    16,
                    16);
            }

            context.DrawEllipse(
                _chainMetalBrush,
                new Pen(strokeBrush, isActive ? 4 : 2),
                joint,
                isActive ? 7 : 6,
                isActive ? 7 : 6);
        }
    }

    private void DrawCelebration(DrawingContext context)
    {
        if (_celebrationProgress <= 0d || _targetPoints.Count == 0)
        {
            return;
        }

        var center = GetTargetCentroid();
        var baseRadius = Math.Min(Bounds.Width, Bounds.Height) * 0.1;
        var ringProgress = EaseOut(_celebrationProgress);
        var ringPen = new Pen(
            new SolidColorBrush(Color.FromArgb(
                (byte)(90 * (1d - _celebrationProgress)),
                _accentColor.R,
                _accentColor.G,
                _accentColor.B)),
            4);

        context.DrawEllipse(
            null,
            ringPen,
            center,
            baseRadius + (ringProgress * 90),
            (baseRadius * 0.72) + (ringProgress * 64));

        var sparkleFill = new SolidColorBrush(Color.FromArgb(
            (byte)(200 * (1d - (_celebrationProgress * 0.55))),
            255,
            251,
            235));
        var sparklePen = new Pen(
            new SolidColorBrush(Color.FromArgb(
                (byte)(150 * (1d - _celebrationProgress)),
                _accentColor.R,
                _accentColor.G,
                _accentColor.B)),
            2);

        for (var index = 0; index < 10; index += 1)
        {
            var angle = (-Math.PI / 2d) + (index * Math.PI / 5d) + (_celebrationProgress * 0.35d);
            var distance = baseRadius + 26 + (_celebrationProgress * (48 + (index % 3) * 12));
            var sparkleCenter = new Point(
                center.X + Math.Cos(angle) * distance,
                center.Y + Math.Sin(angle) * distance * 0.82);
            var radius = 4 + ((index % 3) * 1.5) + ((1d - _celebrationProgress) * 2.5);

            context.DrawEllipse(sparkleFill, sparklePen, sparkleCenter, radius, radius);
        }
    }

    private Point GetTargetCentroid()
    {
        var sumX = 0d;
        var sumY = 0d;
        foreach (var point in _targetPoints)
        {
            var screenPoint = WorldToScreen(point);
            sumX += screenPoint.X;
            sumY += screenPoint.Y;
        }

        return new Point(sumX / _targetPoints.Count, sumY / _targetPoints.Count);
    }

    private static StreamGeometry BuildHexTileGeometry(Point center, double radius)
    {
        var geometry = new StreamGeometry();
        using var geometryContext = geometry.Open();
        geometryContext.BeginFigure(GetHexVertex(center, radius, 0), true);

        for (var index = 1; index < 6; index += 1)
        {
            geometryContext.LineTo(GetHexVertex(center, radius, index));
        }

        geometryContext.EndFigure(true);
        return geometry;
    }

    private static Point GetHexVertex(Point center, double radius, int vertexIndex)
    {
        var angle = -Math.PI / 2d + (vertexIndex * Math.PI / 3d);
        return new Point(
            center.X + Math.Cos(angle) * radius,
            center.Y + Math.Sin(angle) * radius);
    }

    private static void IncludePoint(
        Point point,
        ref double minX,
        ref double maxX,
        ref double minY,
        ref double maxY)
    {
        if (point.X < minX)
        {
            minX = point.X;
        }

        if (point.X > maxX)
        {
            maxX = point.X;
        }

        if (point.Y < minY)
        {
            minY = point.Y;
        }

        if (point.Y > maxY)
        {
            maxY = point.Y;
        }
    }

    private static Color ParseColor(string hexColor, Color fallback)
    {
        return Color.TryParse(hexColor, out var parsedColor)
            ? parsedColor
            : fallback;
    }

    private static Color Mix(Color left, Color right, double amount)
    {
        var clamped = Math.Clamp(amount, 0d, 1d);
        static byte Blend(byte a, byte b, double t) => (byte)Math.Clamp(Math.Round(a + ((b - a) * t)), 0, 255);

        return Color.FromArgb(
            Blend(left.A, right.A, clamped),
            Blend(left.R, right.R, clamped),
            Blend(left.G, right.G, clamped),
            Blend(left.B, right.B, clamped));
    }

    private static Point Project(Point worldPoint)
    {
        return new Point(
            worldPoint.X + (worldPoint.Y * 0.5),
            worldPoint.Y * HexHeight);
    }

    private static double EaseOut(double t)
    {
        var clamped = Math.Clamp(t, 0d, 1d);
        return 1d - Math.Pow(1d - clamped, 3d);
    }
}
