namespace ChainPuzzle.Core;

/// <summary>
/// Defines a single puzzle level: a goal state, a start state, a target silhouette,
/// an optimal move count (par), and metadata such as title and accent colour.
/// </summary>
public sealed class ChainLevel
{
    private readonly HashSet<IntPoint> _targetPointSet;

    public ChainLevel(
        string id,
        string title,
        string subtitle,
        string description,
        string accentHex,
        ChainState goalState,
        ChainState startState,
        int optimalMoves,
        LevelValidation? validation = null,
        IReadOnlyList<IntPoint>? targetPoints = null)
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
        Description = description;
        AccentHex = accentHex;
        GoalState = goalState;
        StartState = startState;
        OptimalMoves = optimalMoves;
        Validation = validation;
        TargetPoints = targetPoints?.ToArray() ?? goalState.GetPoints().ToArray();
        _targetPointSet = TargetPoints.ToHashSet();
    }

    public string Id { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Description { get; }

    public string AccentHex { get; }

    public ChainState GoalState { get; }

    public ChainState StartState { get; }

    public int OptimalMoves { get; }

    public LevelValidation? Validation { get; }

    public IReadOnlyList<IntPoint> TargetPoints { get; }

    public int SegmentCount => GoalState.SegmentCount;

    public bool IsSolved(ChainState state)
    {
        return CoversTarget(state);
    }

    public bool CoversTarget(ChainState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var points = state.GetPoints();
        if (points.Count != _targetPointSet.Count)
        {
            return false;
        }

        foreach (var point in points)
        {
            if (!_targetPointSet.Contains(point))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsWithinTarget(ChainState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        foreach (var point in state.GetPoints())
        {
            if (!_targetPointSet.Contains(point))
            {
                return false;
            }
        }

        return true;
    }

    public int CountTargetOverlap(ChainState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var overlap = 0;
        foreach (var point in state.GetPoints())
        {
            if (_targetPointSet.Contains(point))
            {
                overlap += 1;
            }
        }

        return overlap;
    }

    public bool IsPlayableState(ChainState state)
    {
        return state.IsSelfAvoiding() && IsWithinTarget(state);
    }

    public ChainLevel WithValidation(LevelValidation validation)
    {
        return new ChainLevel(
            Id,
            Title,
            Subtitle,
            Description,
            AccentHex,
            GoalState,
            StartState,
            OptimalMoves,
            validation,
            TargetPoints);
    }
}
