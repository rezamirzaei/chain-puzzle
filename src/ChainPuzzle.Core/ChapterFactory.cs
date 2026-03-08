namespace ChainPuzzle.Core;

/// <summary>
/// Factory for the built-in set of handcrafted puzzle chapters. Each chapter defines
/// a goal shape, a deceptive start configuration, and metadata. Results are cached
/// after the first call.
/// </summary>
public static class ChapterFactory
{
    private sealed record ChapterBlueprint(
        string Id,
        string Title,
        string Subtitle,
        string Description,
        string AccentHex,
        int OptimalMoves,
        LevelTreeProfile TreeProfile,
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
            Profile(new[] { 1, 9, 73, 463, 2650 }, 14, 36, 4, 30, 5, 25, 25),
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 4, 4, 5, 3, 1, 1, 1 },
            new[] { 5, 0, 0, 4, 5, 5, 4, 2, 1, 3, 4, 4, 5, 5, 0, 5, 3, 3, 3 }),
        new(
            "chapter-02",
            "Chapter 2",
            "Terrace",
            "Broad rows hide the route; the shape looks open, but only one full cover survives the turns.",
            "#ef4444",
            6,
            Profile(new[] { 1, 14, 125, 912, 5496 }, 26, 36, 4, 33, 4, 29, 29),
            new[] { 4, 0, 1, 1, 2, 4, 4, 4, 3, 1, 2, 1, 3, 3, 5, 3, 1, 0, 0 },
            new[] { 4, 4, 5, 5, 0, 0, 0, 0, 5, 4, 5, 4, 0, 0, 2, 0, 5, 4, 4 }),
        new(
            "chapter-03",
            "Chapter 3",
            "Basin",
            "Most of the shape is thick and honest; the punishment comes from the wrong internal order.",
            "#f59e0b",
            7,
            Profile(new[] { 1, 12, 97, 613, 3266 }, 20, 36, 4, 27, 3, 24, 24),
            new[] { 0, 5, 5, 3, 2, 0, 2, 4, 3, 1, 3, 4, 5, 4, 0, 4, 2, 1, 2 },
            new[] { 0, 0, 0, 5, 4, 2, 4, 5, 0, 4, 5, 0, 1, 0, 2, 0, 5, 4, 5 }),
        new(
            "chapter-04",
            "Chapter 4",
            "Pyramid",
            "Compact and brutal: broad rows, very few obvious improvements, and many ways to jam the finish.",
            "#14b8a6",
            7,
            Profile(new[] { 1, 12, 93, 551, 2948 }, 27, 37, 4, 32, 4, 28, 28),
            new[] { 1, 0, 4, 4, 3, 1, 1, 3, 4, 4, 4, 0, 0, 4, 3, 5, 1, 1, 5 },
            new[] { 1, 1, 0, 0, 5, 5, 5, 5, 0, 0, 0, 2, 2, 0, 5, 0, 2, 2, 0 }),
        new(
            "chapter-05",
            "Chapter 5",
            "Citadel",
            "Wide walls make the target fair. The hard part is keeping the chain order alive through the center.",
            "#4b5563",
            8,
            Profile(new[] { 1, 9, 80, 606, 3474 }, 30, 37, 5, 29, 5, 24, 24),
            new[] { 2, 4, 4, 2, 1, 5, 1, 3, 2, 0, 1, 5, 5, 1, 5, 4, 4, 2, 4 },
            new[] { 2, 2, 2, 1, 0, 5, 1, 2, 2, 0, 1, 0, 0, 2, 0, 5, 5, 4, 0 })
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
            treeProfile: blueprint.TreeProfile,
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

    private static LevelTreeProfile Profile(
        int[] goalShellCounts,
        int nearTargetDecoyCount,
        int bestDecoyOverlap,
        int startOverlap,
        int startLegalMoveCount,
        int startCloserMoveCount,
        int startTrapMoveCount,
        int startFalseProgressMoveCount)
    {
        return new LevelTreeProfile(
            goalShellCounts,
            nearTargetDecoyCount,
            bestDecoyOverlap,
            startOverlap,
            startLegalMoveCount,
            startCloserMoveCount,
            startTrapMoveCount,
            startFalseProgressMoveCount);
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
