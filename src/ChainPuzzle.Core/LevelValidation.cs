namespace ChainPuzzle.Core;

/// <summary>
/// Validation results produced by <see cref="LevelValidator"/> for a single level.
/// </summary>
/// <param name="HasSolution">Whether a solution path exists from start to goal.</param>
/// <param name="ShortestPathLength">Length of the shortest path, or <see cref="int.MaxValue"/> if none exists.</param>
/// <param name="SolutionCount">Number of distinct full-cover placements of the chain on the target (should be 1).</param>
public sealed record LevelValidation(
    bool HasSolution,
    int ShortestPathLength,
    int SolutionCount);
