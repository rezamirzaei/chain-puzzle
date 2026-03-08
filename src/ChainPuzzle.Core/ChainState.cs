using System.Text;

namespace ChainPuzzle.Core;

/// <summary>
/// Represents the current spatial configuration of a chain: an ordered sequence of
/// <see cref="ChainSegment"/> values defining direction and length. Provides point
/// computation, self-avoidance checks, serialisation, and rotation operations.
/// </summary>
public sealed class ChainState : IEquatable<ChainState>
{
    private readonly ChainSegment[] _segments;
    private IReadOnlyList<IntPoint>? _points;
    private IReadOnlyList<IntPoint>? _jointPoints;
    private IReadOnlyList<int>? _jointPointIndices;
    private IReadOnlyList<SegmentSpan>? _segmentSpans;
    private int[]? _prefixLengths;
    private string? _segmentSignature;
    private string? _pointSignature;
    private int? _cachedHashCode;

    public ChainState(IEnumerable<ChainSegment> segments)
    {
        if (segments is null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        _segments = segments.ToArray();
        if (_segments.Length == 0)
        {
            throw new ArgumentException("A chain must have at least one segment.", nameof(segments));
        }

        if (_segments.Any(segment => segment.Length < 1))
        {
            throw new ArgumentException("Segment lengths must be at least 1.", nameof(segments));
        }
    }

    public int SegmentCount => _segments.Length;

    public int TotalLinks => EnsurePrefixLengths()[^1];

    public IReadOnlyList<ChainSegment> Segments => _segments;

    public static ChainState FromLetters(string letters)
    {
        return FromPattern(letters);
    }

    public static ChainState FromPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Direction source must not be empty.", nameof(pattern));
        }

        var tokens = pattern
            .Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("Direction source must not be empty.", nameof(pattern));
        }

        var segments = new List<ChainSegment>(tokens.Length);
        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            var letterPart = new string(trimmed.TakeWhile(char.IsLetter).ToArray());
            var numberPart = new string(trimmed.SkipWhile(char.IsLetter).ToArray());

            if (string.IsNullOrWhiteSpace(letterPart))
            {
                throw new ArgumentException($"Invalid direction token: {token}", nameof(pattern));
            }

            var direction = DirectionExtensions.ParseToken(letterPart);
            var length = 1;
            if (!string.IsNullOrWhiteSpace(numberPart) && int.TryParse(numberPart, out var parsed))
            {
                length = parsed;
            }

            segments.Add(new ChainSegment(direction, length));
        }

        return new ChainState(segments);
    }

    public static ChainState FromDirectionsWithLengths(string directions, IReadOnlyList<int> lengths)
    {
        if (string.IsNullOrWhiteSpace(directions))
        {
            throw new ArgumentException("Direction source must not be empty.", nameof(directions));
        }

        if (lengths is null)
        {
            throw new ArgumentNullException(nameof(lengths));
        }

        var tokens = directions
            .Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            throw new ArgumentException("Direction source must not be empty.", nameof(directions));
        }

        if (tokens.Length != lengths.Count)
        {
            throw new ArgumentException("Directions and lengths must have the same number of segments.");
        }

        var segments = new ChainSegment[tokens.Length];
        for (var i = 0; i < tokens.Length; i += 1)
        {
            segments[i] = new ChainSegment(DirectionExtensions.ParseToken(tokens[i]), lengths[i]);
        }

        return new ChainState(segments);
    }

    public ChainState Clone()
    {
        return new ChainState(_segments);
    }

    public IReadOnlyList<IntPoint> GetPoints()
    {
        if (_points is not null)
        {
            return _points;
        }

        var points = new List<IntPoint>(TotalLinks + 1)
        {
            new IntPoint(0, 0)
        };

        var cursor = new IntPoint(0, 0);
        foreach (var segment in _segments)
        {
            var step = segment.Direction.ToVector();
            for (var count = 0; count < segment.Length; count += 1)
            {
                cursor += step;
                points.Add(cursor);
            }
        }

        _points = points;
        return _points;
    }

    public IReadOnlyList<IntPoint> GetJointPoints()
    {
        if (_jointPoints is not null)
        {
            return _jointPoints;
        }

        var points = new List<IntPoint>(Math.Max(0, SegmentCount - 1));
        var cursor = new IntPoint(0, 0);
        for (var index = 0; index < _segments.Length; index += 1)
        {
            var segment = _segments[index];
            var step = segment.Direction.ToVector();
            for (var count = 0; count < segment.Length; count += 1)
            {
                cursor += step;
            }

            if (index < _segments.Length - 1)
            {
                points.Add(cursor);
            }
        }

        _jointPoints = points;
        return _jointPoints;
    }

    public IReadOnlyList<int> GetJointPointIndices()
    {
        if (_jointPointIndices is not null)
        {
            return _jointPointIndices;
        }

        var prefix = EnsurePrefixLengths();
        var jointIndices = new int[Math.Max(0, SegmentCount - 1)];
        for (var i = 1; i < prefix.Length - 1; i += 1)
        {
            jointIndices[i - 1] = prefix[i];
        }

        _jointPointIndices = jointIndices;
        return _jointPointIndices;
    }

    public IReadOnlyList<SegmentSpan> GetSegmentSpans()
    {
        if (_segmentSpans is not null)
        {
            return _segmentSpans;
        }

        var prefix = EnsurePrefixLengths();
        var spans = new SegmentSpan[_segments.Length];
        for (var index = 0; index < _segments.Length; index += 1)
        {
            spans[index] = new SegmentSpan(prefix[index], prefix[index + 1], _segments[index].Length);
        }

        _segmentSpans = spans;
        return _segmentSpans;
    }

    public int GetPointIndexForJoint(int jointIndex)
    {
        if (jointIndex < 1 || jointIndex >= SegmentCount)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        var prefix = EnsurePrefixLengths();
        return prefix[jointIndex];
    }

    public bool IsSelfAvoiding()
    {
        var occupied = new HashSet<IntPoint>();
        foreach (var point in GetPoints())
        {
            if (!occupied.Add(point))
            {
                return false;
            }
        }

        return true;
    }

    public string SerializeSegments()
    {
        if (_segmentSignature is not null)
        {
            return _segmentSignature;
        }

        var buffer = new StringBuilder(_segments.Length * 3);
        foreach (var segment in _segments)
        {
            buffer.Append(segment.Direction.ToLetter());
            if (segment.Length != 1)
            {
                buffer.Append(segment.Length);
            }

            buffer.Append(',');
        }

        _segmentSignature = buffer.ToString();
        return _segmentSignature;
    }

    public string PointSignature()
    {
        if (_pointSignature is not null)
        {
            return _pointSignature;
        }

        var points = GetPoints();
        var buffer = new StringBuilder(points.Count * 8);
        for (var index = 0; index < points.Count; index += 1)
        {
            var point = points[index];
            buffer.Append(point.X);
            buffer.Append(',');
            buffer.Append(point.Y);
            if (index < points.Count - 1)
            {
                buffer.Append('|');
            }
        }

        _pointSignature = buffer.ToString();
        return _pointSignature;
    }

    public ChainState? RotateFromJoint(int jointIndex, int quarterTurns)
    {
        if (jointIndex < 1 || jointIndex >= SegmentCount)
        {
            return null;
        }

        var nextSegments = (ChainSegment[])_segments.Clone();
        for (var index = jointIndex; index < nextSegments.Length; index += 1)
        {
            nextSegments[index] = nextSegments[index].Rotate(quarterTurns);
        }

        var nextState = new ChainState(nextSegments);
        return nextState.IsSelfAvoiding() ? nextState : null;
    }

    private int[] EnsurePrefixLengths()
    {
        if (_prefixLengths is not null)
        {
            return _prefixLengths;
        }

        var prefix = new int[_segments.Length + 1];
        for (var index = 0; index < _segments.Length; index += 1)
        {
            prefix[index + 1] = prefix[index] + _segments[index].Length;
        }

        _prefixLengths = prefix;
        return _prefixLengths;
    }

    /// <summary>Determines structural equality based on segment directions and lengths.</summary>
    public bool Equals(ChainState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_segments.Length != other._segments.Length) return false;
        for (var i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] != other._segments[i]) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ChainState);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (_cachedHashCode.HasValue) return _cachedHashCode.Value;
        var hash = new HashCode();
        foreach (var seg in _segments)
        {
            hash.Add(seg.Direction);
            hash.Add(seg.Length);
        }
        _cachedHashCode = hash.ToHashCode();
        return _cachedHashCode.Value;
    }
}
