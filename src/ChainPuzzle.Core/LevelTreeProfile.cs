namespace ChainPuzzle.Core;

public sealed record LevelTreeProfile(
    IReadOnlyList<int> GoalShellCounts,
    int NearTargetDecoyCount,
    int BestDecoyOverlap,
    int StartOverlap,
    int StartLegalMoveCount,
    int StartCloserMoveCount,
    int StartTrapMoveCount);
