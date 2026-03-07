using ChainPuzzle.Core;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace ChainPuzzle.Tests;

public sealed class ChainCoreTests
{
    private static readonly IntPoint[] NeighborOffsets =
    {
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    };

    [Fact]
    public void InvalidRotationsAreRejectedWhenLinksCollide()
    {
        var state = new ChainState(new[]
        {
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.East, 1),
            new ChainSegment(Direction.NorthEast, 1),
            new ChainSegment(Direction.West, 1),
            new ChainSegment(Direction.West, 1)
        });

        var invalidMove = state.RotateFromJoint(1, 1);
        Assert.Null(invalidMove);

        var validMove = state.RotateFromJoint(1, -1);
        Assert.NotNull(validMove);
        Assert.True(validMove!.IsSelfAvoiding());
    }

    [Fact]
    public void ChaptersStartUnsolved()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        Assert.True(levels.Count >= 6);

        foreach (var level in levels)
        {
            Assert.False(level.IsSolved(level.StartState));
        }
    }

    [Fact]
    public void ChaptersExposeLegalMoves()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);

        var level = levels[0];
        var solver = new ChainSolver(level.SegmentCount);
        var moves = solver.GetLegalMoves(level.StartState);
        Assert.NotEmpty(moves);

        var applied = level.StartState.RotateFromJoint(moves[0].Move.JointIndex, moves[0].Move.Rotation);
        Assert.NotNull(applied);
        Assert.True(applied!.IsSelfAvoiding());
    }

    [Fact]
    public void ChapterTargetMatchesSolvedLayout()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        foreach (var level in levels)
        {
            var goalSet = level.GoalState.GetPoints().ToHashSet();
            var targetSet = level.TargetPoints.ToHashSet();

            Assert.Equal(goalSet.Count, targetSet.Count);
            Assert.True(goalSet.SetEquals(targetSet));
        }
    }

    [Fact]
    public void ChaptersAreSolvableWithinHintBudget()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        foreach (var level in levels)
        {
            var solver = new ChainSolver(level.SegmentCount);
            var path = solver.FindShortestPath(level.StartState, level.IsSolved, maxVisited: 60_000);

            Assert.True(path is not null, $"Expected a path for {level.Id}.");
            Assert.NotEmpty(path!);
        }
    }

    [Fact]
    public void ChapterGameSupportsUndoAndRedo()
    {
        var game = new ChapterGame(ChapterFactory.CreateChapters(validate: false));
        var startSignature = game.CurrentState.PointSignature();

        var solver = new ChainSolver(game.CurrentLevel.SegmentCount);
        var move = solver.GetLegalMoves(game.CurrentState).First().Move;

        var moved = game.TryRotate(move.JointIndex, move.Rotation, out var movedState);
        Assert.True(moved);
        Assert.NotEqual(startSignature, movedState.PointSignature());
        Assert.Equal(1, game.Moves);
        Assert.True(game.CanUndo);
        Assert.False(game.CanRedo);

        var undone = game.TryUndo();
        Assert.True(undone);
        Assert.Equal(startSignature, game.CurrentState.PointSignature());
        Assert.Equal(0, game.Moves);
        Assert.False(game.CanUndo);
        Assert.True(game.CanRedo);

        var redone = game.TryRedo();
        Assert.True(redone);
        Assert.Equal(movedState.PointSignature(), game.CurrentState.PointSignature());
        Assert.Equal(1, game.Moves);
        Assert.True(game.CanUndo);
    }

    [Fact]
    public void ChapterTargetsAreConnectedFilledAreas()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        foreach (var level in levels)
        {
            var target = level.TargetPoints.ToHashSet();
            Assert.NotEmpty(target);
            Assert.True(IsConnected(target), $"Target for {level.Id} must be connected.");

            var holes = CountHoles(target);
            Assert.True(holes == 0, $"Target for {level.Id} must not contain holes: holes={holes}");

            var boundary = CountBoundaryEdges(target);
            Assert.True(boundary <= 100, $"Target for {level.Id} is too line-like: boundary={boundary}");

            var interiorCount = CountInteriorPoints(target);
            Assert.True(interiorCount >= 20, $"Target for {level.Id} is not filled enough: interior={interiorCount}");
        }
    }

    [Fact]
    public void ChapterTargetsAreDistinctUpToHexSymmetry()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        var signatures = new HashSet<string>();

        foreach (var level in levels)
        {
            var signature = CanonicalHexShapeSignature(level.TargetPoints);
            Assert.True(signatures.Add(signature), $"Duplicate target shape (up to rotation/reflection): {level.Id}");
        }
    }

    [Fact]
    public void ChapterTargetsAreNotNearDuplicates()
    {
        var levels = ChapterFactory.CreateChapters(validate: false);
        for (var i = 0; i < levels.Count; i += 1)
        {
            for (var j = i + 1; j < levels.Count; j += 1)
            {
                var overlap = BestOverlap(levels[i].TargetPoints, levels[j].TargetPoints);
                Assert.True(
                    overlap <= 24,
                    $"Targets too similar: {levels[i].Id} vs {levels[j].Id} overlap={overlap}");
            }
        }
    }

    private static bool IsConnected(HashSet<IntPoint> target)
    {
        var queue = new Queue<IntPoint>();
        var visited = new HashSet<IntPoint>();
        var start = target.First();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var offset in NeighborOffsets)
            {
                var next = current + offset;
                if (!target.Contains(next) || !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        return visited.Count == target.Count;
    }

    private static int CountInteriorPoints(HashSet<IntPoint> target)
    {
        var count = 0;
        foreach (var point in target)
        {
            var neighbors = 0;
            foreach (var offset in NeighborOffsets)
            {
                if (target.Contains(point + offset))
                {
                    neighbors += 1;
                }
            }

            if (neighbors >= 4)
            {
                count += 1;
            }
        }

        return count;
    }

    private static int CountBoundaryEdges(HashSet<IntPoint> target)
    {
        var boundary = 0;
        foreach (var point in target)
        {
            foreach (var offset in NeighborOffsets)
            {
                if (!target.Contains(point + offset))
                {
                    boundary += 1;
                }
            }
        }

        return boundary;
    }

    private static int CountHoles(HashSet<IntPoint> target)
    {
        var xs = target.Select(point => point.X).ToArray();
        var ys = target.Select(point => point.Y).ToArray();

        var minX = xs.Min() - 2;
        var maxX = xs.Max() + 2;
        var minY = ys.Min() - 2;
        var maxY = ys.Max() + 2;

        bool InBounds(IntPoint point)
        {
            return point.X >= minX
                && point.X <= maxX
                && point.Y >= minY
                && point.Y <= maxY;
        }

        var outside = new HashSet<IntPoint>();
        var queue = new Queue<IntPoint>();
        var start = new IntPoint(minX, minY);
        queue.Enqueue(start);
        outside.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var offset in NeighborOffsets)
            {
                var next = current + offset;
                if (!InBounds(next) || target.Contains(next) || !outside.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        var visited = new HashSet<IntPoint>(outside);
        var holes = 0;
        for (var x = minX; x <= maxX; x += 1)
        {
            for (var y = minY; y <= maxY; y += 1)
            {
                var point = new IntPoint(x, y);
                if (target.Contains(point) || visited.Contains(point))
                {
                    continue;
                }

                holes += 1;
                queue.Enqueue(point);
                visited.Add(point);

                while (queue.Count > 0)
                {
                    var cursor = queue.Dequeue();
                    foreach (var offset in NeighborOffsets)
                    {
                        var next = cursor + offset;
                        if (!InBounds(next) || target.Contains(next) || !visited.Add(next))
                        {
                            continue;
                        }

                        queue.Enqueue(next);
                    }
                }
            }
        }

        return holes;
    }

    private static string CanonicalHexShapeSignature(IEnumerable<IntPoint> points)
    {
        var source = points.ToArray();
        var variants = new List<string>(12);

        foreach (var reflected in new[] { false, true })
        {
            for (var rotation = 0; rotation < 6; rotation += 1)
            {
                var transformed = source
                    .Select(point => TransformHex(point, rotation, reflected))
                    .ToArray();

                var minX = transformed.Min(point => point.X);
                var minY = transformed.Min(point => point.Y);

                var normalized = transformed
                    .Select(point => new IntPoint(point.X - minX, point.Y - minY))
                    .OrderBy(point => point.X)
                    .ThenBy(point => point.Y)
                    .Select(point => $"{point.X},{point.Y}");

                variants.Add(string.Join("|", normalized));
            }
        }

        return variants.Min(StringComparer.Ordinal)!;
    }

    private static int BestOverlap(IReadOnlyList<IntPoint> aPoints, IReadOnlyList<IntPoint> bPoints)
    {
        var aSet = aPoints.ToHashSet();
        var aArray = aPoints.ToArray();
        var bArray = bPoints.ToArray();

        var best = 0;
        foreach (var reflected in new[] { false, true })
        {
            for (var rotation = 0; rotation < 6; rotation += 1)
            {
                var transformed = bArray
                    .Select(point => TransformHex(point, rotation, reflected))
                    .ToArray();

                foreach (var anchorA in aArray)
                {
                    foreach (var anchorB in transformed)
                    {
                        var dx = anchorA.X - anchorB.X;
                        var dy = anchorA.Y - anchorB.Y;

                        var overlap = 0;
                        foreach (var point in transformed)
                        {
                            if (aSet.Contains(new IntPoint(point.X + dx, point.Y + dy)))
                            {
                                overlap += 1;
                            }
                        }

                        if (overlap > best)
                        {
                            best = overlap;
                            if (best == aSet.Count)
                            {
                                return best;
                            }
                        }
                    }
                }
            }
        }

        return best;
    }

    private static IntPoint TransformHex(IntPoint point, int rotations, bool reflected)
    {
        var (x, y, z) = AxialToCube(point);
        if (reflected)
        {
            var reflectedX = x;
            var reflectedY = z;
            var reflectedZ = y;
            (x, y, z) = (reflectedX, reflectedY, reflectedZ);
        }

        for (var i = 0; i < rotations; i += 1)
        {
            (x, y, z) = (-z, -x, -y);
        }

        return new IntPoint(x, z);
    }

    private static (int X, int Y, int Z) AxialToCube(IntPoint point)
    {
        var x = point.X;
        var z = point.Y;
        var y = -x - z;
        return (x, y, z);
    }
}
