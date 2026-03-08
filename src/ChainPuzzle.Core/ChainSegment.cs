namespace ChainPuzzle.Core;

/// <summary>
/// A single segment of a chain, defined by a direction and a link count.
/// </summary>
/// <param name="Direction">The hex-grid direction this segment extends in.</param>
/// <param name="Length">The number of links (cells) the segment occupies.</param>
public readonly record struct ChainSegment(Direction Direction, int Length)
{
    /// <summary>Returns a new segment rotated by the given number of 60° steps.</summary>
    /// <param name="quarterTurns">Number of hex-direction steps to rotate (positive = clockwise).</param>
    public ChainSegment Rotate(int quarterTurns)
    {
        return new ChainSegment(Direction.Rotate(quarterTurns), Length);
    }
}
