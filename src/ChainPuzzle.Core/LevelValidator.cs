namespace ChainPuzzle.Core;

public sealed class LevelValidator
{
    private const int MaxVisited = 1_000_000;

    private readonly ChainSolver _solver;

    public LevelValidator(ChainSolver solver)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
    }

    public LevelValidation Analyze(ChainLevel level)
    {
        if (level is null)
        {
            throw new ArgumentNullException(nameof(level));
        }

        var shortestPath = _solver.FindShortestPath(
            level.StartState,
            level.IsSolved,
            maxVisited: MaxVisited);
        var hasSolution = shortestPath is not null;
        var solutionCount = hasSolution
            ? _solver.CountGoalStates(
                level.StartState,
                level.IsSolved,
                stopAfter: 2,
                maxVisited: MaxVisited)
            : 0;

        return new LevelValidation(
            hasSolution,
            hasSolution ? shortestPath!.Count : int.MaxValue,
            solutionCount);
    }

    public LevelValidation AssertUnique(ChainLevel level)
    {
        var analysis = Analyze(level);
        if (!analysis.HasSolution)
        {
            throw new InvalidOperationException($"Level \"{level.Title}\" has no solution.");
        }

        if (analysis.SolutionCount != 1)
        {
            throw new InvalidOperationException(
                $"Level \"{level.Title}\" must have exactly one solution state, found {analysis.SolutionCount}.");
        }

        return analysis;
    }
}
