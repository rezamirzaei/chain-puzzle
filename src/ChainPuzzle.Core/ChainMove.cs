namespace ChainPuzzle.Core;

public readonly record struct ChainMove(int JointIndex, int Rotation)
{
    public ChainMove Inverse()
    {
        return new ChainMove(JointIndex, -Rotation);
    }
}
