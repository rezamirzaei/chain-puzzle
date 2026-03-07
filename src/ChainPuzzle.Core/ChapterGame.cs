namespace ChainPuzzle.Core;

public sealed class ChapterGame
{
    private sealed record HistoryEntry(ChainState State, int Moves);

    private readonly Dictionary<int, ChainSolver> _solvers = new();
    private readonly Stack<HistoryEntry> _undoHistory = new();
    private readonly Stack<HistoryEntry> _redoHistory = new();

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

    public bool CanUndo => _undoHistory.Count > 0;

    public bool CanRedo => _redoHistory.Count > 0;

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
        _undoHistory.Clear();
        _redoHistory.Clear();
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

        _undoHistory.Push(new HistoryEntry(CurrentState, Moves));
        _redoHistory.Clear();
        CurrentState = candidate;
        nextState = candidate;
        Moves += 1;

        if (IsSolved)
        {
            CompletedLevelIds.Add(CurrentLevel.Id);
        }

        return true;
    }

    public bool TryUndo()
    {
        if (_undoHistory.Count == 0)
        {
            return false;
        }

        _redoHistory.Push(new HistoryEntry(CurrentState, Moves));
        var previous = _undoHistory.Pop();
        CurrentState = previous.State;
        Moves = previous.Moves;
        return true;
    }

    public bool TryRedo()
    {
        if (_redoHistory.Count == 0)
        {
            return false;
        }

        _undoHistory.Push(new HistoryEntry(CurrentState, Moves));
        var next = _redoHistory.Pop();
        CurrentState = next.State;
        Moves = next.Moves;

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
            maxVisited: 1_000_000);
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
