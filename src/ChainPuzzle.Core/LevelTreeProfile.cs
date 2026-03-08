namespace ChainPuzzle.Core;

/// <summary>
/// Tree-profile metrics for a level, describing goal-shell breadth
/// and start-state deceptiveness without the full analysis overhead.
/// </summary>
/// <param name="GoalShellCounts">State counts at each BFS depth from the goal.</param>
/// <param name="NearTargetDecoyCount">Number of near-target decoy states found.</param>
/// <param name="BestDecoyOverlap">Highest overlap between any decoy state and the target.</param>
/// <param name="StartOverlap">Target-point overlap of the start state.</param>
/// <param name="StartLegalMoveCount">Total legal moves available from the start.</param>
/// <param name="StartCloserMoveCount">How many start moves get closer to goal.</param>
/// <param name="StartTrapMoveCount">How many start moves are traps (move further from goal).</param>
public sealed record LevelTreeProfile(
    IReadOnlyList<int> GoalShellCounts,
    int NearTargetDecoyCount,
    int BestDecoyOverlap,
    int StartOverlap,
    int StartLegalMoveCount,
    int StartCloserMoveCount,
    int StartTrapMoveCount);
