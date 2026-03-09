using ChainPuzzle.Core;
using Xunit;

namespace ChainPuzzle.Tests;

public sealed class ChapterCandidateTest
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
    public void ReplacementChaptersStayCompact()
    {
        var levels = ChapterFactory.CreateChapters(validate: false).Skip(1).ToArray();

        Assert.Equal(9, levels.Length);
        foreach (var level in levels)
        {
            var target = level.TargetPoints.ToHashSet();
            var boundary = CountBoundaryEdges(target);
            var interior = CountInteriorPoints(target);
            var lowDegree = CountLowDegreePoints(target);

            Assert.True(boundary <= 72, $"{level.Id} boundary too large: {boundary}");
            Assert.True(interior >= 29, $"{level.Id} interior too low: {interior}");
            Assert.True(lowDegree <= 5, $"{level.Id} low-degree count too high: {lowDegree}");
        }
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

    private static int CountInteriorPoints(HashSet<IntPoint> target)
    {
        var count = 0;
        foreach (var point in target)
        {
            var degree = 0;
            foreach (var offset in NeighborOffsets)
            {
                if (target.Contains(point + offset))
                {
                    degree += 1;
                }
            }

            if (degree >= 4)
            {
                count += 1;
            }
        }

        return count;
    }

    private static int CountLowDegreePoints(HashSet<IntPoint> target)
    {
        var count = 0;
        foreach (var point in target)
        {
            var degree = 0;
            foreach (var offset in NeighborOffsets)
            {
                if (target.Contains(point + offset))
                {
                    degree += 1;
                }
            }

            if (degree <= 2)
            {
                count += 1;
            }
        }

        return count;
    }
}
