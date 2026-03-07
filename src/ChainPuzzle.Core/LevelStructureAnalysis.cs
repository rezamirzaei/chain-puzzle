namespace ChainPuzzle.Core;

public sealed record LevelStructureAnalysis(
    int CoverSolutionCount,
    int ShortestCoverPathLength,
    IReadOnlyList<int> GoalShellCounts,
    int NearTargetDecoyCount,
    int BestDecoyOverlap,
    int StartOverlap,
    int StartLegalMoveCount,
    int StartCloserMoveCount,
    int StartTrapMoveCount)
{
    public bool HasUniqueCover => CoverSolutionCount == 1;
}
