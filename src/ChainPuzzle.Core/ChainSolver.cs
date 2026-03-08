namespace ChainPuzzle.Core;

/// <summary>
/// BFS-based solver for chain puzzles. Enumerates legal moves (single-joint rotations
/// that preserve self-avoidance), finds shortest paths to goal states, and counts
/// the number of reachable goal states.
/// </summary>
public sealed class ChainSolver
{
    private sealed record ParentEdge(string FromKey, ChainMove Move);

    public sealed record LegalMove(ChainMove Move, ChainState NextState);

    public ChainSolver(int segmentCount)
    {
        if (segmentCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentCount), "A chain must have at least two segments.");
        }

        SegmentCount = segmentCount;
    }

    public int SegmentCount { get; }

    public IReadOnlyList<LegalMove> GetLegalMoves(
        ChainState state,
        Func<ChainState, bool>? isStateAllowed = null)
    {
        var moves = new List<LegalMove>(state.SegmentCount * 2);

        for (var jointIndex = 1; jointIndex < state.SegmentCount; jointIndex += 1)
        {
            foreach (var rotation in new[] { -1, 1 })
            {
                var nextState = state.RotateFromJoint(jointIndex, rotation);
                if (nextState is null)
                {
                    continue;
                }

                if (isStateAllowed is not null && !isStateAllowed(nextState))
                {
                    continue;
                }

                moves.Add(new LegalMove(new ChainMove(jointIndex, rotation), nextState));
            }
        }

        return moves;
    }

    public IReadOnlyList<ChainMove>? FindShortestPath(
        ChainState startState,
        Func<ChainState, bool> isGoal,
        int maxVisited = int.MaxValue,
        Func<ChainState, bool>? isStateAllowed = null)
    {
        if (startState is null)
        {
            throw new ArgumentNullException(nameof(startState));
        }
        if (isGoal is null)
        {
            throw new ArgumentNullException(nameof(isGoal));
        }
        if (isStateAllowed is not null && !isStateAllowed(startState))
        {
            return null;
        }

        if (isGoal(startState))
        {
            return Array.Empty<ChainMove>();
        }

        var startKey = startState.SerializeSegments();
        var queue = new Queue<ChainState>();
        queue.Enqueue(startState);

        var visited = new HashSet<string> { startKey };
        var parents = new Dictionary<string, ParentEdge>();

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            var stateKey = state.SerializeSegments();

            foreach (var legalMove in GetLegalMoves(state, isStateAllowed))
            {
                var nextKey = legalMove.NextState.SerializeSegments();
                if (!visited.Add(nextKey))
                {
                    continue;
                }

                if (visited.Count > maxVisited)
                {
                    return null;
                }

                parents[nextKey] = new ParentEdge(stateKey, legalMove.Move);
                if (isGoal(legalMove.NextState))
                {
                    return ReconstructMoves(nextKey, parents);
                }

                queue.Enqueue(legalMove.NextState);
            }
        }

        return null;
    }

    public int CountGoalStates(
        ChainState startState,
        Func<ChainState, bool> isGoal,
        int stopAfter = int.MaxValue,
        int maxVisited = int.MaxValue,
        Func<ChainState, bool>? isStateAllowed = null)
    {
        if (startState is null)
        {
            throw new ArgumentNullException(nameof(startState));
        }
        if (isGoal is null)
        {
            throw new ArgumentNullException(nameof(isGoal));
        }
        if (isStateAllowed is not null && !isStateAllowed(startState))
        {
            return 0;
        }

        var queue = new Queue<ChainState>();
        queue.Enqueue(startState);

        var visited = new HashSet<string> { startState.SerializeSegments() };
        var count = isGoal(startState) ? 1 : 0;
        if (count >= stopAfter)
        {
            return count;
        }

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            foreach (var legalMove in GetLegalMoves(state, isStateAllowed))
            {
                var nextKey = legalMove.NextState.SerializeSegments();
                if (!visited.Add(nextKey))
                {
                    continue;
                }

                if (visited.Count > maxVisited)
                {
                    return count;
                }

                if (isGoal(legalMove.NextState))
                {
                    count += 1;
                    if (count >= stopAfter)
                    {
                        return count;
                    }
                }

                queue.Enqueue(legalMove.NextState);
            }
        }

        return count;
    }

    private static IReadOnlyList<ChainMove> ReconstructMoves(
        string goalKey,
        IReadOnlyDictionary<string, ParentEdge> parents)
    {
        var path = new List<ChainMove>();
        var cursor = goalKey;
        while (parents.TryGetValue(cursor, out var edge))
        {
            path.Add(edge.Move);
            cursor = edge.FromKey;
        }

        path.Reverse();
        return path;
    }
}
