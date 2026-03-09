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

    // 19 segments: lengths 3,3,2,3,1,2,2,1,1,2,2,2,2,2,1,2,2,2,2 (total links = 37)
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
            "Fit the chain into the fir-tree pattern. Most folds improve coverage, so explore freely.",
            "#d97706",
            6,
            Profile(new[] { 1, 9, 73, 463, 2650 }, 14, 36, 4, 30, 5, 25, 25),
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 4, 4, 5, 3, 1, 1, 1 },
            new[] { 5, 0, 0, 4, 5, 5, 4, 2, 1, 3, 4, 4, 5, 5, 0, 5, 3, 3, 3 }),
        new(
            "chapter-02",
            "Chapter 2",
            "Lantern",
            "A compact lantern with many legal-looking folds. Spend the middle too early and the last corner stays outside.",
            "#ef4444",
            6,
            Profile(new[] { 1, 11, 102, 649, 3466 }, 12, 36, 4, 28, 4, 24, 24),
            new[] { 5, 0, 0, 4, 5, 3, 2, 0, 5, 1, 3, 3, 5, 3, 5, 3, 1, 1, 2 },
            new[] { 5, 1, 1, 5, 0, 0, 5, 4, 3, 5, 0, 0, 2, 0, 2, 1, 5, 5, 0 }),
        new(
            "chapter-03",
            "Chapter 3",
            "Forge",
            "The center feels roomy, but the rim is not. Keep one escape lane alive for the finish.",
            "#f59e0b",
            6,
            Profile(new[] { 1, 11, 103, 690, 3970 }, 12, 35, 5, 29, 4, 25, 25),
            new[] { 5, 1, 1, 5, 0, 4, 3, 1, 0, 2, 4, 4, 3, 3, 4, 2, 0, 0, 2 },
            new[] { 5, 5, 5, 3, 4, 4, 3, 2, 1, 3, 5, 5, 4, 4, 5, 4, 2, 2, 4 }),
        new(
            "chapter-04",
            "Chapter 4",
            "Bastion",
            "A dense wall with several believable fits. The wrong hinge order leaves the outer face one step short.",
            "#14b8a6",
            6,
            Profile(new[] { 1, 11, 100, 687, 3901 }, 22, 36, 4, 32, 5, 27, 27),
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 3, 3, 5, 3, 1, 0, 0 },
            new[] { 5, 0, 0, 4, 5, 5, 4, 3, 2, 4, 5, 5, 0, 0, 2, 0, 5, 4, 4 }),
        new(
            "chapter-05",
            "Chapter 5",
            "Harbor",
            "Wide enough to tempt greedy coverage, tight enough to punish it. Save the swing on the far side.",
            "#4b5563",
            6,
            Profile(new[] { 1, 11, 85, 524, 2767 }, 18, 36, 4, 28, 3, 25, 25),
            new[] { 5, 1, 3, 1, 2, 0, 5, 3, 2, 4, 0, 0, 2, 2, 3, 1, 5, 5, 5 },
            new[] { 5, 0, 2, 0, 1, 5, 5, 3, 3, 5, 0, 0, 1, 1, 2, 1, 5, 5, 5 }),
        new(
            "chapter-06",
            "Chapter 6",
            "Anchor",
            "Balanced and deceptive. Only a careful rebuild of the middle keeps the final branch open.",
            "#7c3aed",
            6,
            Profile(new[] { 1, 11, 94, 683, 3815 }, 1, 35, 4, 26, 1, 25, 25),
            new[] { 5, 4, 5, 3, 4, 2, 1, 5, 4, 0, 2, 1, 3, 1, 3, 1, 5, 5, 4 },
            new[] { 5, 5, 0, 5, 0, 4, 3, 1, 1, 3, 4, 3, 5, 3, 5, 3, 2, 3, 2 }),
        new(
            "chapter-07",
            "Chapter 7",
            "Citadel",
            "A late-game keep where almost every move still sits inside the map. Only one route preserves the last pocket.",
            "#dc2626",
            6,
            Profile(new[] { 1, 11, 112, 776, 4452 }, 23, 36, 4, 32, 5, 27, 27),
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 2, 4, 5, 3, 1, 1, 1 },
            new[] { 5, 0, 0, 4, 5, 5, 4, 3, 2, 4, 5, 5, 5, 1, 2, 0, 5, 5, 5 }),
        new(
            "chapter-08",
            "Chapter 8",
            "Vault",
            "Dense from the first fold. Many openings improve locally, but the wrong route strands the last two links.",
            "#0891b2",
            6,
            Profile(new[] { 1, 12, 113, 747, 4091 }, 19, 36, 4, 29, 3, 26, 26),
            new[] { 5, 3, 5, 3, 4, 2, 1, 5, 4, 0, 2, 0, 2, 2, 3, 1, 5, 5, 5 },
            new[] { 5, 4, 0, 4, 5, 4, 3, 2, 2, 3, 5, 3, 5, 5, 0, 5, 3, 3, 3 }),
        new(
            "chapter-09",
            "Chapter 9",
            "Granary",
            "Looks solved before it is. One bad commitment turns the final chamber into dead space.",
            "#be185d",
            6,
            Profile(new[] { 1, 11, 129, 979, 6464 }, 3, 36, 4, 29, 1, 28, 28),
            new[] { 5, 5, 0, 4, 5, 3, 2, 0, 5, 1, 3, 2, 2, 2, 3, 1, 5, 5, 5 },
            new[] { 5, 0, 1, 0, 1, 5, 4, 2, 2, 4, 5, 4, 4, 4, 5, 4, 2, 3, 3 }),
        new(
            "chapter-10",
            "Chapter 10",
            "Cathedral",
            "The broadest dense map in the set. Plenty of legal folds stay inside the target, but only one finish seals the frame.",
            "#1e3a5f",
            6,
            Profile(new[] { 1, 12, 129, 890, 5247 }, 27, 37, 5, 29, 4, 25, 25),
            new[] { 5, 1, 1, 5, 0, 4, 3, 1, 0, 2, 4, 4, 5, 5, 4, 2, 2, 2, 2 },
            new[] { 5, 4, 4, 3, 4, 2, 2, 0, 5, 1, 3, 3, 4, 4, 3, 2, 2, 2, 2 })
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
