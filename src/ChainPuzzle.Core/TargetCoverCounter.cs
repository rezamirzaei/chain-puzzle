namespace ChainPuzzle.Core;

/// <summary>
/// Counts how many distinct ways a chain with given segment lengths can fully cover
/// a target set of hex-grid points, using back-tracking search.
/// Used to verify that each puzzle has exactly one solution placement.
/// </summary>
public sealed class TargetCoverCounter
{
    private static readonly Direction[] AllDirections =
    {
        Direction.East,
        Direction.NorthEast,
        Direction.NorthWest,
        Direction.West,
        Direction.SouthWest,
        Direction.SouthEast
    };

    private readonly int[] _segmentLengths;
    private readonly int _totalPointCount;

    public TargetCoverCounter(IEnumerable<int> segmentLengths)
    {
        if (segmentLengths is null)
        {
            throw new ArgumentNullException(nameof(segmentLengths));
        }

        _segmentLengths = segmentLengths.ToArray();
        if (_segmentLengths.Length == 0)
        {
            throw new ArgumentException("At least one segment length is required.", nameof(segmentLengths));
        }

        if (_segmentLengths.Any(length => length < 1))
        {
            throw new ArgumentException("Segment lengths must be positive.", nameof(segmentLengths));
        }

        _totalPointCount = _segmentLengths.Sum() + 1;
    }

    public int CountSolutions(IReadOnlyCollection<IntPoint> targetPoints, int stopAfter = int.MaxValue)
    {
        if (targetPoints is null)
        {
            throw new ArgumentNullException(nameof(targetPoints));
        }

        if (stopAfter < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stopAfter));
        }

        if (targetPoints.Count != _totalPointCount)
        {
            return 0;
        }

        var targetSet = targetPoints.ToHashSet();
        if (targetSet.Count != targetPoints.Count)
        {
            return 0;
        }

        var origin = new IntPoint(0, 0);
        if (!targetSet.Contains(origin))
        {
            return 0;
        }

        var visited = new HashSet<IntPoint> { origin };
        return CountFromSegment(0, origin, targetSet, visited, stopAfter);
    }

    private int CountFromSegment(
        int segmentIndex,
        IntPoint cursor,
        IReadOnlySet<IntPoint> targetSet,
        HashSet<IntPoint> visited,
        int stopAfter)
    {
        if (segmentIndex == _segmentLengths.Length)
        {
            return visited.Count == _totalPointCount ? 1 : 0;
        }

        var remainingBudget = stopAfter;
        var count = 0;
        var segmentLength = _segmentLengths[segmentIndex];
        foreach (var direction in AllDirections)
        {
            var step = direction.ToVector();
            var pointsAdded = 0;
            var next = cursor;
            var valid = true;
            for (var i = 0; i < segmentLength; i += 1)
            {
                next += step;
                if (!targetSet.Contains(next) || !visited.Add(next))
                {
                    valid = false;
                    break;
                }

                pointsAdded += 1;
            }

            if (valid)
            {
                var branchCount = CountFromSegment(segmentIndex + 1, next, targetSet, visited, remainingBudget);
                count += branchCount;
                remainingBudget -= branchCount;
                if (remainingBudget <= 0)
                {
                    RemoveTrail(visited, cursor, step, pointsAdded);
                    return count;
                }
            }

            RemoveTrail(visited, cursor, step, pointsAdded);
        }

        return count;
    }

    private static void RemoveTrail(
        HashSet<IntPoint> visited,
        IntPoint cursor,
        IntPoint step,
        int pointsAdded)
    {
        var next = cursor;
        for (var i = 0; i < pointsAdded; i += 1)
        {
            next += step;
            visited.Remove(next);
        }
    }
}
