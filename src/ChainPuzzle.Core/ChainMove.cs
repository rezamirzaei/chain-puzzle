namespace ChainPuzzle.Core;

/// <summary>
/// Describes a single player move: which joint to rotate, and in which direction.
/// </summary>
/// <param name="JointIndex">The 1-based index of the joint to rotate at.</param>
/// <param name="Rotation">The rotation step (-1 = counter-clockwise, +1 = clockwise).</param>
public readonly record struct ChainMove(int JointIndex, int Rotation)
{
    /// <summary>Returns the move that undoes this one (same joint, opposite rotation).</summary>
    public ChainMove Inverse()
    {
        return new ChainMove(JointIndex, -Rotation);
    }
}
