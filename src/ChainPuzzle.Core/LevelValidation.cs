namespace ChainPuzzle.Core;

public sealed record LevelValidation(
    bool HasSolution,
    int ShortestPathLength,
    int SolutionCount);
