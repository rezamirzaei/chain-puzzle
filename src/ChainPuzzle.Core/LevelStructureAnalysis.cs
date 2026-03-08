namespace ChainPuzzle.Core;

/// <summary>
/// Full structural analysis of a level, including cover solution count,
/// shortest path length, goal-shell breadth, decoy statistics, and start-state metrics.
/// </summary>
/// <param name="CoverSolutionCount">Number of unique full-cover solutions.</param>
/// <param name="ShortestCoverPathLength">Shortest move-path length from start to goal.</param>
/// <param name="GoalShellCounts">Number of unique states at each BFS depth from goal (shell counts).</param>
/// <param name="NearTargetDecoyCount">How many states near the target exist that are not the solution.</param>
/// <param name="BestDecoyOverlap">Highest target overlap found among decoy states.</param>
/// <param name="StartOverlap">How many target points the start state already covers.</param>
/// <param name="StartLegalMoveCount">Total number of legal moves from the start state.</param>
/// <param name="StartCloserMoveCount">Number of moves from start that move closer to the goal.</param>
/// <param name="StartTrapMoveCount">Number of moves from start that lead away from the goal (traps).</param>
/// <param name="StartFalseProgressMoveCount">Number of start moves that keep or improve overlap without getting closer.</param>
public sealed record LevelStructureAnalysis(
    int CoverSolutionCount,
    int ShortestCoverPathLength,
    IReadOnlyList<int> GoalShellCounts,
    int NearTargetDecoyCount,
    int BestDecoyOverlap,
    int StartOverlap,
    int StartLegalMoveCount,
    int StartCloserMoveCount,
    int StartTrapMoveCount,
    int StartFalseProgressMoveCount)
{
    /// <summary>Whether the level has exactly one full-cover solution.</summary>
    public bool HasUniqueCover => CoverSolutionCount == 1;
}
