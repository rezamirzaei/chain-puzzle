namespace ChainPuzzle.Core;

public static class ChapterFactory
{
    private sealed record ChapterBlueprint(
        string Id,
        string Title,
        string Subtitle,
        string Description,
        string AccentHex,
        int OptimalMoves,
        int[] GoalDirections,
        int[] StartDirections);

    private static readonly Lazy<IReadOnlyList<ChainLevel>> CachedChapters =
        new(() => CreateChaptersInternal(validate: false));

    // 19 segments: 12x2, 3x3, 4x1 (total occupied links = 37)
    private static readonly int[] SegmentLengths =
    {
        3, 3, 2, 3, 1, 2, 2, 1, 1, 2, 2, 2, 2, 2, 1, 2, 2, 2, 2
    };

    private static readonly ChapterBlueprint[] Blueprints =
    {
        new(
            "chapter-01",
            "Chapter 1",
            "Christmas Tree",
            "Fit the mixed 1/2/3-link loop into the fir-tree pattern.",
            "#d97706",
            6,
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 4, 4, 5, 3, 1, 1, 1 },
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 4, 4, 3, 1, 1, 3, 3 }),
        new(
            "chapter-02",
            "Chapter 2",
            "Lightning Bolt",
            "Cover the long zigzag bolt without getting trapped near the heavy base.",
            "#ef4444",
            6,
            new[] { 1, 1, 2, 1, 5, 4, 5, 4, 4, 4, 4, 5, 5, 5, 3, 2, 2, 3, 2 },
            new[] { 1, 1, 2, 1, 5, 4, 5, 4, 4, 4, 4, 5, 5, 5, 3, 2, 4, 2, 2 }),
        new(
            "chapter-03",
            "Chapter 3",
            "Iron Claw",
            "Route the chain through the hooked claw without wasting space in the throat.",
            "#0ea5e9",
            6,
            new[] { 1, 2, 2, 3, 5, 0, 5, 5, 5, 4, 4, 2, 4, 2, 4, 2, 0, 2, 0 },
            new[] { 1, 2, 2, 3, 5, 5, 5, 0, 0, 4, 4, 2, 4, 2, 4, 2, 0, 2, 2 }),
        new(
            "chapter-04",
            "Chapter 4",
            "Bastion",
            "Fill the broad wall and its lower spur without collapsing the middle lanes.",
            "#22c55e",
            6,
            new[] { 4, 4, 5, 4, 5, 0, 4, 2, 3, 2, 1, 3, 3, 2, 1, 5, 0, 0, 2 },
            new[] { 4, 4, 5, 4, 5, 0, 4, 2, 3, 2, 1, 3, 3, 3, 2, 1, 5, 0, 2 }),
        new(
            "chapter-05",
            "Chapter 5",
            "Gatehouse",
            "The crown looks open, but the lower passage still decides whether the cover works.",
            "#8b5cf6",
            6,
            new[] { 4, 5, 3, 3, 2, 0, 0, 2, 3, 1, 1, 2, 2, 0, 1, 3, 1, 3, 4 },
            new[] { 4, 5, 3, 3, 2, 0, 0, 2, 3, 2, 0, 1, 2, 1, 1, 3, 1, 3, 4 }),
        new(
            "chapter-06",
            "Chapter 6",
            "Bell Tower",
            "A tall tower with a heavy roofline and very little room to unwind mistakes.",
            "#f59e0b",
            6,
            new[] { 1, 0, 0, 4, 3, 1, 3, 5, 4, 2, 4, 4, 5, 4, 3, 5, 1, 0, 4 },
            new[] { 1, 0, 0, 4, 3, 1, 3, 5, 4, 2, 4, 4, 5, 4, 3, 5, 0, 1, 2 }),
        new(
            "chapter-07",
            "Chapter 7",
            "Falcon Wing",
            "The swept top edge reads cleanly, but the inner body still has several dead continuations.",
            "#14b8a6",
            6,
            new[] { 1, 3, 4, 4, 0, 1, 1, 0, 4, 4, 4, 4, 4, 2, 4, 5, 0, 4, 0 },
            new[] { 1, 3, 4, 4, 5, 1, 1, 1, 5, 4, 4, 4, 4, 2, 4, 5, 0, 4, 4 }),
        new(
            "chapter-08",
            "Chapter 8",
            "Mushroom Cap",
            "The cap looks generous, but the stem alignment still forces the full-cover route.",
            "#ec4899",
            6,
            new[] { 0, 5, 3, 3, 2, 0, 0, 2, 3, 3, 3, 4, 5, 5, 3, 2, 2, 2, 3 },
            new[] { 0, 5, 3, 3, 2, 0, 0, 2, 3, 3, 3, 4, 5, 4, 3, 1, 2, 2, 1 }),
        new(
            "chapter-09",
            "Chapter 9",
            "Key",
            "A long shaft, a dense head, and almost no forgiveness in the last few placements.",
            "#4b5563",
            6,
            new[] { 5, 3, 5, 5, 5, 0, 5, 5, 0, 2, 2, 3, 2, 2, 0, 0, 5, 1, 0 },
            new[] { 5, 3, 5, 5, 5, 0, 5, 5, 0, 2, 2, 3, 2, 2, 0, 0, 1, 5, 4 }),
        new(
            "chapter-10",
            "Chapter 10",
            "Anchor",
            "Final exam: the bottom body is dense, the stem is narrow, and the last turns are unforgiving.",
            "#84cc16",
            6,
            new[] { 5, 5, 5, 1, 0, 1, 3, 5, 3, 4, 2, 2, 2, 1, 2, 0, 0, 1, 2 },
            new[] { 5, 5, 5, 1, 5, 0, 2, 3, 3, 4, 2, 2, 2, 1, 2, 0, 0, 1, 0 })
    };

    public static IReadOnlyList<ChainLevel> CreateChapters(bool validate = false)
    {
        return validate ? CreateChaptersInternal(validate: true) : CachedChapters.Value;
    }

    private static IReadOnlyList<ChainLevel> CreateChaptersInternal(bool validate)
    {
        var levels = new List<ChainLevel>(Blueprints.Length);
        foreach (var blueprint in Blueprints)
        {
            levels.Add(CreateLevel(blueprint, validate));
        }

        return levels;
    }

    private static ChainLevel CreateLevel(ChapterBlueprint blueprint, bool validate)
    {
        var goalState = BuildState(blueprint.GoalDirections);
        var startState = BuildState(blueprint.StartDirections);

        if (goalState.SegmentCount != startState.SegmentCount)
        {
            throw new InvalidOperationException(
                $"Level \"{blueprint.Id}\": goal and start must have the same segment count.");
        }

        if (!goalState.IsSelfAvoiding())
        {
            throw new InvalidOperationException($"Level \"{blueprint.Id}\": goal state must be self-avoiding.");
        }

        if (!startState.IsSelfAvoiding())
        {
            throw new InvalidOperationException($"Level \"{blueprint.Id}\": start state must be self-avoiding.");
        }

        if (CoversSameTarget(goalState, startState))
        {
            throw new InvalidOperationException(
                $"Level \"{blueprint.Id}\": start must not already cover the target.");
        }

        var solver = new ChainSolver(goalState.SegmentCount);
        if (solver.GetLegalMoves(startState).Count == 0)
        {
            throw new InvalidOperationException($"Level \"{blueprint.Id}\": start state has no legal moves.");
        }

        var level = new ChainLevel(
            blueprint.Id,
            blueprint.Title,
            blueprint.Subtitle,
            blueprint.Description,
            blueprint.AccentHex,
            goalState,
            startState,
            blueprint.OptimalMoves,
            targetPoints: goalState.GetPoints().ToArray());

        if (!validate)
        {
            return level;
        }

        var validator = new LevelValidator(solver);
        var validation = validator.Analyze(level);
        return level.WithValidation(validation);
    }

    private static ChainState BuildState(int[] directionIndices)
    {
        if (directionIndices.Length != SegmentLengths.Length)
        {
            throw new InvalidOperationException("Direction list must match segment length count.");
        }

        var segments = new ChainSegment[SegmentLengths.Length];
        for (var index = 0; index < SegmentLengths.Length; index += 1)
        {
            segments[index] = new ChainSegment(ToDirection(directionIndices[index]), SegmentLengths[index]);
        }

        return new ChainState(segments);
    }

    private static Direction ToDirection(int index)
    {
        return index switch
        {
            0 => Direction.East,
            1 => Direction.NorthEast,
            2 => Direction.NorthWest,
            3 => Direction.West,
            4 => Direction.SouthWest,
            5 => Direction.SouthEast,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Unsupported direction index.")
        };
    }

    private static bool CoversSameTarget(ChainState goalState, ChainState startState)
    {
        var goalPoints = goalState.GetPoints().ToHashSet();
        var startPoints = startState.GetPoints();
        if (goalPoints.Count != startPoints.Count)
        {
            return false;
        }

        foreach (var point in startPoints)
        {
            if (!goalPoints.Contains(point))
            {
                return false;
            }
        }

        return true;
    }
}
