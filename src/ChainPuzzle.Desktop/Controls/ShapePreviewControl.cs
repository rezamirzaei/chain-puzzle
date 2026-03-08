using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ChainPuzzle.Core;

namespace ChainPuzzle.Desktop.Controls;

/// <summary>
/// A compact hex-grid thumbnail control that renders a chapter's target silhouette.
/// Used in the home overlay chapter gallery.
/// </summary>
public sealed class ShapePreviewControl : Control
{
    private const double HexHeight = 0.8660254037844386; // sqrt(3) / 2

    private readonly List<IntPoint> _targetPoints = new();

    private Color _accentColor = Colors.SteelBlue;
    private bool _isCurrent;

    private double _scale = 1d;
    private double _offsetX;
    private double _offsetY;

    public ShapePreviewControl()
    {
        ClipToBounds = true;
    }

    public void UpdatePreview(
        IReadOnlyList<IntPoint> targetPoints,
        string accentHex,
        bool isCurrent)
    {
        _targetPoints.Clear();
        foreach (var targetPoint in targetPoints)
        {
            _targetPoints.Add(targetPoint);
        }

        _accentColor = ParseColor(accentHex, _accentColor);
        _isCurrent = isCurrent;
        RecalculateTransform();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        RecalculateTransform();
        DrawBackground(context);
        DrawTarget(context);
    }

    private void RecalculateTransform()
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1 || _targetPoints.Count == 0)
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

        foreach (var point in _targetPoints)
        {
            IncludePoint(Project(new Point(point.X, point.Y)), ref minX, ref maxX, ref minY, ref maxY);
        }

        var width = Math.Max(1d, maxX - minX);
        var height = Math.Max(1d, maxY - minY);

        var horizontalPadding = 38d;
        var verticalPadding = 38d;
        var scaleX = (Bounds.Width - horizontalPadding) / width;
        var scaleY = (Bounds.Height - verticalPadding) / height;
        _scale = Math.Max(10d, Math.Min(scaleX, scaleY));

        _offsetX = (Bounds.Width / 2d) - ((minX + maxX) / 2d) * _scale;
        _offsetY = (Bounds.Height / 2d) - ((minY + maxY) / 2d) * _scale;
    }

    private void DrawBackground(DrawingContext context)
    {
        var mist = Mix(_accentColor, Colors.White, 0.88);
        var wash = Mix(_accentColor, ParseColor("#F59E0B", Colors.Goldenrod), 0.22);
        var background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(255, mist.R, mist.G, mist.B), 0),
                new GradientStop(Color.FromArgb(255, wash.R, wash.G, wash.B), 1)
            }
        };

        context.DrawRectangle(background, null, new Rect(0, 0, Bounds.Width, Bounds.Height));
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(_isCurrent ? (byte)48 : (byte)28, _accentColor.R, _accentColor.G, _accentColor.B)),
            null,
            new Point(Bounds.Width * 0.26, Bounds.Height * 0.24),
            Bounds.Width * 0.22,
            Bounds.Width * 0.22);
        context.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(_isCurrent ? (byte)40 : (byte)22, wash.R, wash.G, wash.B)),
            null,
            new Point(Bounds.Width * 0.78, Bounds.Height * 0.74),
            Bounds.Width * 0.2,
            Bounds.Width * 0.2);
    }

    private void DrawTarget(DrawingContext context)
    {
        if (_targetPoints.Count == 0)
        {
            return;
        }

        var warmAccent = Mix(_accentColor, ParseColor("#F59E0B", Colors.Goldenrod), 0.18);
        var tileBrush = new SolidColorBrush(Color.FromArgb(218, warmAccent.R, warmAccent.G, warmAccent.B));
        var tilePen = new Pen(
            new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            _isCurrent ? 1.6 : 1.1);
        var highlightBrush = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255));
        var glowBrush = new SolidColorBrush(Color.FromArgb(_isCurrent ? (byte)60 : (byte)36, _accentColor.R, _accentColor.G, _accentColor.B));

        var glowRadius = _scale * 0.58;
        var tileRadius = _scale * 0.51;
        var highlightRadius = _scale * 0.29;

        foreach (var point in _targetPoints)
        {
            var center = WorldToScreen(point);
            context.DrawGeometry(glowBrush, null, BuildHexTileGeometry(center, glowRadius));
            context.DrawGeometry(tileBrush, tilePen, BuildHexTileGeometry(center, tileRadius));
            context.DrawGeometry(highlightBrush, null, BuildHexTileGeometry(center, highlightRadius));
        }
    }

    private Point WorldToScreen(IntPoint worldPoint)
    {
        var projected = Project(new Point(worldPoint.X, worldPoint.Y));
        return new Point(
            projected.X * _scale + _offsetX,
            projected.Y * _scale + _offsetY);
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
}
