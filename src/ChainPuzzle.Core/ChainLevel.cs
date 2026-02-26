namespace ChainPuzzle.Core;

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
        Validation = validation;
        TargetPoints = targetPoints?.ToArray() ?? goalState.GetPoints().ToArray();
        _targetPointSet = TargetPoints.ToHashSet();
        GoalPointSignature = goalState.PointSignature();
    }

    public string Id { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Description { get; }

    public string AccentHex { get; }

    public ChainState GoalState { get; }

    public ChainState StartState { get; }

    public LevelValidation? Validation { get; }

    public IReadOnlyList<IntPoint> TargetPoints { get; }

    public int SegmentCount => GoalState.SegmentCount;

    private string GoalPointSignature { get; }

    public bool IsSolved(ChainState state)
    {
        return state.PointSignature() == GoalPointSignature;
    }

    public bool IsWithinTarget(ChainState state)
    {
        foreach (var point in state.GetPoints())
        {
            if (!_targetPointSet.Contains(point))
            {
                return false;
            }
        }

        return true;
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
            validation,
            TargetPoints);
    }
}
