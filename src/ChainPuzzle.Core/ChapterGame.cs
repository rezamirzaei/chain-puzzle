namespace ChainPuzzle.Core;

public sealed class ChapterGame
{
    private readonly Dictionary<int, ChainSolver> _solvers = new();

    public ChapterGame(IReadOnlyList<ChainLevel> levels)
    {
        if (levels is null)
        {
            throw new ArgumentNullException(nameof(levels));
        }
        if (levels.Count == 0)
        {
            throw new ArgumentException("At least one chapter is required.", nameof(levels));
        }

        Levels = levels;
        CurrentState = levels[0].StartState.Clone();
    }

    public IReadOnlyList<ChainLevel> Levels { get; }

    public int LevelIndex { get; private set; }

    public ChainLevel CurrentLevel => Levels[LevelIndex];

    public ChainState CurrentState { get; private set; }

    public int Moves { get; private set; }

    public HashSet<string> CompletedLevelIds { get; } = new();

    public bool IsSolved => CurrentLevel.IsSolved(CurrentState);

    public void SetLevel(int levelIndex)
    {
        LevelIndex = Math.Clamp(levelIndex, 0, Levels.Count - 1);
        ResetLevel();
    }

    public void NextLevel()
    {
        SetLevel(LevelIndex + 1);
    }

    public void PreviousLevel()
    {
        SetLevel(LevelIndex - 1);
    }

    public void ResetLevel()
    {
        CurrentState = CurrentLevel.StartState.Clone();
        Moves = 0;
    }

    public bool TryRotate(int jointIndex, int rotation, out ChainState nextState)
    {
        nextState = CurrentState;
        if (IsSolved)
        {
            return false;
        }

        var candidate = CurrentState.RotateFromJoint(jointIndex, rotation);
        if (candidate is null)
        {
            return false;
        }

        CurrentState = candidate;
        nextState = candidate;
        Moves += 1;

        if (IsSolved)
        {
            CompletedLevelIds.Add(CurrentLevel.Id);
        }

        return true;
    }

    public ChainMove? GetHintMove()
    {
        if (IsSolved)
        {
            return null;
        }

        var solver = GetSolver(CurrentLevel.SegmentCount);
        var path = solver.FindShortestPath(
            CurrentState,
            CurrentLevel.IsSolved,
            maxVisited: 60_000);
        if (path is null || path.Count == 0)
        {
            return null;
        }

        return path[0];
    }

    private ChainSolver GetSolver(int segmentCount)
    {
        if (_solvers.TryGetValue(segmentCount, out var existing))
        {
            return existing;
        }

        var solver = new ChainSolver(segmentCount);
        _solvers[segmentCount] = solver;
        return solver;
    }
}
