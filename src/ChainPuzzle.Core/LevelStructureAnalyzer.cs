namespace ChainPuzzle.Core;

/// <summary>
/// Produces detailed structural metrics for a level by performing BFS from the goal
/// and analysing tree breadth, decoy states, and start-state deceptiveness.
/// </summary>
public sealed class LevelStructureAnalyzer
{
    private sealed record DistanceEntry(ChainState State, int Depth);

    private readonly ChainSolver _solver;

    public LevelStructureAnalyzer(ChainSolver solver)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
    }

    public LevelStructureAnalysis Analyze(
        ChainLevel level,
        int shellDepth = 4,
        int nearTargetSlack = 4,
        int maxVisited = 1_000_000)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        if (shellDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shellDepth));
        }

        if (nearTargetSlack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nearTargetSlack));
        }

        var tree = AnalyzeTree(level, shellDepth, nearTargetSlack, maxVisited);

        var shortestPath = _solver.FindShortestPath(
            level.StartState,
            level.IsSolved,
            maxVisited: maxVisited);
        var shortestCoverPathLength = shortestPath?.Count ?? int.MaxValue;
        var coverSolutionCount = shortestPath is null
            ? 0
            : new TargetCoverCounter(level.GoalState.Segments.Select(segment => segment.Length))
                .CountSolutions(level.TargetPoints, stopAfter: 2);

        return new LevelStructureAnalysis(
            coverSolutionCount,
            shortestCoverPathLength,
            tree.GoalShellCounts,
            tree.NearTargetDecoyCount,
            tree.BestDecoyOverlap,
            tree.StartOverlap,
            tree.StartLegalMoveCount,
            tree.StartCloserMoveCount,
            tree.StartTrapMoveCount,
            tree.StartFalseProgressMoveCount);
    }

    public LevelTreeProfile AnalyzeTree(
        ChainLevel level,
        int shellDepth = 4,
        int nearTargetSlack = 4,
        int maxVisited = 1_000_000)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        if (shellDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shellDepth));
        }

        if (nearTargetSlack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nearTargetSlack));
        }

        var searchDepth = Math.Max(shellDepth, Math.Max(0, level.OptimalMoves - 1));
        var goalDistances = BuildGoalDistanceMap(level.GoalState, searchDepth, maxVisited);
        var goalShellCounts = BuildGoalShellCounts(goalDistances, shellDepth);

        var nearTargetThreshold = Math.Max(0, level.TargetPoints.Count - nearTargetSlack);
        var nearTargetDecoyCount = 0;
        var bestDecoyOverlap = 0;
        foreach (var entry in goalDistances.Values)
        {
            if (entry.Depth == 0)
            {
                continue;
            }

            var overlap = level.CountTargetOverlap(entry.State);
            if (overlap > bestDecoyOverlap)
            {
                bestDecoyOverlap = overlap;
            }

            if (overlap >= nearTargetThreshold)
            {
                nearTargetDecoyCount += 1;
            }
        }

        var startDepth = level.OptimalMoves;
        var startKey = level.StartState.SerializeSegments();
        if (goalDistances.TryGetValue(startKey, out var startEntry))
        {
            startDepth = startEntry.Depth;
        }

        var startLegalMoveCount = 0;
        var startCloserMoveCount = 0;
        var startTrapMoveCount = 0;
        var startFalseProgressMoveCount = 0;
        var startOverlap = level.CountTargetOverlap(level.StartState);
        foreach (var legalMove in _solver.GetLegalMoves(level.StartState))
        {
            startLegalMoveCount += 1;
            var nextKey = legalMove.NextState.SerializeSegments();
            var getsCloser = goalDistances.TryGetValue(nextKey, out var nextEntry) && nextEntry.Depth < startDepth;
            if (getsCloser)
            {
                startCloserMoveCount += 1;
            }
            else
            {
                startTrapMoveCount += 1;
            }

            var nextOverlap = level.CountTargetOverlap(legalMove.NextState);
            if (nextOverlap >= startOverlap && !getsCloser)
            {
                startFalseProgressMoveCount += 1;
            }
        }

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

    private Dictionary<string, DistanceEntry> BuildGoalDistanceMap(
        ChainState goalState,
        int maxDepth,
        int maxVisited)
    {
        var startKey = goalState.SerializeSegments();
        var discovered = new Dictionary<string, DistanceEntry>
        {
            [startKey] = new DistanceEntry(goalState, 0)
        };

        if (maxDepth == 0)
        {
            return discovered;
        }

        var queue = new Queue<DistanceEntry>();
        queue.Enqueue(new DistanceEntry(goalState, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth >= maxDepth)
            {
                continue;
            }

            foreach (var legalMove in _solver.GetLegalMoves(current.State))
            {
                var nextKey = legalMove.NextState.SerializeSegments();
                if (discovered.ContainsKey(nextKey))
                {
                    continue;
                }

                var nextEntry = new DistanceEntry(legalMove.NextState, current.Depth + 1);
                discovered[nextKey] = nextEntry;
                if (discovered.Count >= maxVisited)
                {
                    return discovered;
                }

                queue.Enqueue(nextEntry);
            }
        }

        return discovered;
    }

    private static IReadOnlyList<int> BuildGoalShellCounts(
        IReadOnlyDictionary<string, DistanceEntry> goalDistances,
        int shellDepth)
    {
        var counts = new int[shellDepth + 1];
        foreach (var entry in goalDistances.Values)
        {
            if (entry.Depth > shellDepth)
            {
                continue;
            }

            counts[entry.Depth] += 1;
        }

        return counts;
    }
}
