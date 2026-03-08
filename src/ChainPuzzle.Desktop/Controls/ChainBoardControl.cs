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

    private readonly List<Point> _chainPoints = new();
    private readonly List<Point> _jointPoints = new();
    private readonly List<SegmentSpan> _segmentSpans = new();
    private readonly List<IntPoint> _targetPoints = new();

    private int? _activeJointIndex;
    private Color _accentColor = ParseColor("#f48c06", Colors.Orange);
    private bool _isSolved;

    private double _scale = 1d;
    private double _offsetX;
    private double _offsetY;

    // Cached brushes/pens — recreated only when accent or solved state changes.
    private SolidColorBrush? _targetGlowBrush;
    private SolidColorBrush? _targetTileBrush;
    private Pen? _targetTilePen;
    private SolidColorBrush? _targetHighlightBrush;
    private Color _cachedAccentForBrushes;
    private bool _cachedSolvedForBrushes;

    public ChainBoardControl()
    {
        ClipToBounds = true;
    }

    public void UpdateScene(
        IReadOnlyList<Point> chainPoints,
        IReadOnlyList<Point> jointPoints,
        IReadOnlyList<SegmentSpan> segmentSpans,
        IReadOnlyList<IntPoint> targetPoints,
        int? activeJointIndex,
        string accentHex,
        bool isSolved)
    {
        _chainPoints.Clear();
        _chainPoints.AddRange(chainPoints);

        _jointPoints.Clear();
        _jointPoints.AddRange(jointPoints);

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

        var bestDistance = double.MaxValue;
        int? bestIndex = null;

        for (var index = 0; index < _jointPoints.Count; index += 1)
        {
            var joint = WorldToScreen(_jointPoints[index]);
            var dx = joint.X - screenPoint.X;
            var dy = joint.Y - screenPoint.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= maxDistance && distance < bestDistance)
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
        else
        {
            foreach (var point in _chainPoints)
            {
                IncludePoint(Project(point), ref minX, ref maxX, ref minY, ref maxY);
            }
        }

        var width = Math.Max(1d, maxX - minX);
        var height = Math.Max(1d, maxY - minY);

        var horizontalPadding = 220d;
        var verticalPadding = 240d;
        var scaleX = (Bounds.Width - horizontalPadding) / width;
        var scaleY = (Bounds.Height - verticalPadding) / height;
        _scale = Math.Max(18d, Math.Min(scaleX, scaleY));

        _offsetX = (Bounds.Width / 2d) - ((minX + maxX) / 2d) * _scale;
        _offsetY = (Bounds.Height / 2d) - ((minY + maxY) / 2d) * _scale;
    }

    private void DrawBackground(DrawingContext context)
    {
        var background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(ParseColor("#E9F5FF", Colors.White), 0),
                new GradientStop(ParseColor("#D8F3DC", Colors.White), 0.45),
                new GradientStop(ParseColor("#FEFAE0", Colors.White), 1)
            }
        };

        context.DrawRectangle(background, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

        var glowBrush = new SolidColorBrush(Color.FromArgb(36, _accentColor.R, _accentColor.G, _accentColor.B));
        context.DrawEllipse(glowBrush, null, new Point(Bounds.Width * 0.2, Bounds.Height * 0.2), Bounds.Width * 0.2, Bounds.Width * 0.2);
        context.DrawEllipse(glowBrush, null, new Point(Bounds.Width * 0.82, Bounds.Height * 0.78), Bounds.Width * 0.17, Bounds.Width * 0.17);
    }

    private void DrawTarget(DrawingContext context)
    {
        if (_targetPoints.Count == 0)
        {
            return;
        }

        EnsureTargetBrushes();
        var glowRadius = _scale * 0.64;
        var tileRadius = _scale * 0.585;
        var highlightRadius = _scale * 0.36;

        foreach (var point in _targetPoints)
        {
            var center = WorldToScreen(point);
            context.DrawGeometry(_targetGlowBrush, null, BuildHexTileGeometry(center, glowRadius));
            context.DrawGeometry(_targetTileBrush, _targetTilePen, BuildHexTileGeometry(center, tileRadius));
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
        _targetTilePen = new Pen(
            new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)235 : (byte)205, _accentColor.R, _accentColor.G, _accentColor.B)),
            1.3);
        _targetHighlightBrush = new SolidColorBrush(Color.FromArgb(_isSolved ? (byte)70 : (byte)42, 255, 255, 255));
    }

    private void DrawChain(DrawingContext context)
    {
        if (_chainPoints.Count < 2 || _segmentSpans.Count == 0)
        {
            return;
        }

        var lightWood = ParseColor("#F4EBD0", Colors.WhiteSmoke);
        var darkWood = ParseColor("#B07D56", Colors.SaddleBrown);
        var metal = ParseColor("#A8A29E", Colors.SlateGray);

        for (var index = 0; index < _segmentSpans.Count; index += 1)
        {
            var span = _segmentSpans[index];
            var start = WorldToScreen(_chainPoints[span.StartIndex]);
            var end = WorldToScreen(_chainPoints[span.EndIndex]);
            var isLight = index % 2 == 0;
            var baseColor = isLight ? lightWood : darkWood;
            var shadow = new SolidColorBrush(Color.FromArgb(60, 20, 20, 20));

            var thickness = 20 + (span.Length - 1) * 3;
            var shadowPen = new Pen(shadow, thickness + 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var segmentPen = new Pen(new SolidColorBrush(baseColor), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 70, 45, 35)), 2, lineCap: PenLineCap.Round);

            context.DrawLine(shadowPen, start, end);
            context.DrawLine(segmentPen, start, end);
            context.DrawLine(edgePen, start, end);
        }

        DrawChainJoints(context, metal);
    }

    private void DrawChainJoints(DrawingContext context, Color metal)
    {
        if (_chainPoints.Count == 0)
        {
            return;
        }

        var root = WorldToScreen(_chainPoints[0]);
        var tip = WorldToScreen(_chainPoints[^1]);
        var rootBrush = new SolidColorBrush(metal);
        var tipBrush = new SolidColorBrush(metal);

        context.DrawEllipse(rootBrush, new Pen(rootBrush, 2), root, 9, 9);
        context.DrawEllipse(tipBrush, new Pen(tipBrush, 2), tip, 7, 7);

        for (var index = 0; index < _jointPoints.Count; index += 1)
        {
            var joint = WorldToScreen(_jointPoints[index]);
            var jointIndex = index + 1;
            var isActive = _activeJointIndex == jointIndex;

            var fillBrush = new SolidColorBrush(metal);
            var strokeBrush = isActive
                ? new SolidColorBrush(ParseColor("#D00000", Colors.Red))
                : new SolidColorBrush(ParseColor("#264653", Colors.DarkSlateGray));

            context.DrawEllipse(
                fillBrush,
                new Pen(strokeBrush, isActive ? 4 : 2),
                joint,
                isActive ? 7 : 6,
                isActive ? 7 : 6);
        }
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

    private static Point Project(Point worldPoint)
    {
        return new Point(
            worldPoint.X + (worldPoint.Y * 0.5),
            worldPoint.Y * HexHeight);
    }
}
