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
            3,
            new[] { 5, 5, 5, 3, 4, 2, 1, 5, 4, 0, 2, 2, 4, 4, 5, 3, 1, 1, 1 },
            new[] { 5, 5, 5, 3, 5, 3, 2, 0, 5, 1, 2, 2, 3, 3, 4, 2, 0, 0, 0 }),
        new(
            "chapter-02",
            "Chapter 2",
            "Filled Shape A",
            "Cover every real dot in this compact filled board using the full linked chain.",
            "#ef4444",
            3,
            new[] { 1, 1, 2, 1, 5, 4, 5, 4, 4, 4, 4, 5, 5, 5, 3, 2, 2, 3, 2 },
            new[] { 1, 2, 3, 2, 1, 0, 1, 0, 0, 0, 5, 0, 0, 0, 4, 3, 3, 4, 3 }),
        new(
            "chapter-03",
            "Chapter 3",
            "Filled Shape B",
            "Route the chain through a dense filled board with no fake interior dots.",
            "#0ea5e9",
            3,
            new[] { 1, 2, 2, 3, 5, 0, 5, 5, 5, 4, 4, 2, 4, 2, 4, 2, 0, 2, 0 },
            new[] { 1, 2, 2, 3, 4, 5, 5, 5, 5, 4, 5, 3, 5, 3, 5, 3, 1, 3, 1 }),
        new(
            "chapter-04",
            "Chapter 4",
            "Filled Shape C",
            "Keep the loop self-avoiding while filling this compact connected area.",
            "#22c55e",
            3,
            new[] { 4, 4, 5, 4, 5, 0, 4, 2, 3, 2, 1, 3, 3, 2, 1, 5, 0, 0, 2 },
            new[] { 4, 4, 5, 4, 4, 5, 3, 2, 3, 2, 1, 3, 3, 3, 2, 0, 1, 1, 3 }),
        new(
            "chapter-05",
            "Chapter 5",
            "Filled Shape D",
            "Use the same links to cover a wider filled board.",
            "#8b5cf6",
            3,
            new[] { 4, 5, 3, 3, 2, 0, 0, 2, 3, 1, 1, 2, 2, 0, 1, 3, 1, 3, 4 },
            new[] { 4, 5, 3, 4, 3, 1, 1, 3, 4, 2, 2, 3, 3, 1, 1, 2, 0, 2, 3 }),
        new(
            "chapter-06",
            "Chapter 6",
            "Filled Shape E",
            "Final chapter: a tall filled shape with tighter turns.",
            "#f59e0b",
            3,
            new[] { 1, 0, 0, 4, 3, 1, 3, 5, 4, 2, 4, 4, 5, 4, 3, 5, 1, 0, 4 },
            new[] { 1, 0, 0, 4, 3, 1, 3, 5, 4, 2, 4, 5, 0, 5, 4, 0, 2, 1, 1 }),
        new(
            "chapter-07",
            "Chapter 7",
            "Filled Shape F",
            "A compact filled silhouette with sharper turns.",
            "#14b8a6",
            3,
            new[] { 1, 3, 4, 4, 0, 1, 1, 0, 4, 4, 4, 4, 4, 2, 4, 5, 0, 4, 0 },
            new[] { 1, 3, 4, 4, 0, 1, 1, 0, 4, 4, 4, 4, 5, 3, 4, 5, 0, 5, 1 }),
        new(
            "chapter-08",
            "Chapter 8",
            "Filled Shape G",
            "Stay smooth through the corners while covering every dot.",
            "#ec4899",
            1,
            new[] { 0, 5, 3, 3, 2, 0, 0, 2, 3, 3, 3, 4, 5, 5, 3, 2, 2, 2, 3 },
            new[] { 0, 5, 3, 3, 2, 0, 0, 2, 3, 3, 3, 3, 4, 4, 2, 1, 1, 1, 2 }),
        new(
            "chapter-09",
            "Chapter 9",
            "Filled Shape H",
            "Keep the loop collision-free while filling this shape.",
            "#4b5563",
            1,
            new[] { 5, 3, 5, 5, 5, 0, 5, 5, 0, 2, 2, 3, 2, 2, 0, 0, 5, 1, 0 },
            new[] { 5, 3, 5, 5, 5, 0, 5, 5, 0, 2, 2, 3, 2, 2, 0, 0, 5, 1, 1 }),
        new(
            "chapter-10",
            "Chapter 10",
            "Filled Shape I",
            "Final exam: the most irregular filled board in the set.",
            "#84cc16",
            3,
            new[] { 5, 5, 5, 1, 0, 1, 3, 5, 3, 4, 2, 2, 2, 1, 2, 0, 0, 1, 2 },
            new[] { 5, 5, 5, 1, 0, 1, 3, 5, 3, 4, 2, 2, 2, 2, 4, 2, 2, 3, 5 })
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

        if (goalState.PointSignature() == startState.PointSignature())
        {
            throw new InvalidOperationException($"Level \"{blueprint.Id}\": start must be different from goal.");
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
}
