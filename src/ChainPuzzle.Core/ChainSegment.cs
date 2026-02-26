namespace ChainPuzzle.Core;

public readonly record struct ChainSegment(Direction Direction, int Length)
{
    public ChainSegment Rotate(int quarterTurns)
    {
        return new ChainSegment(Direction.Rotate(quarterTurns), Length);
    }
}
