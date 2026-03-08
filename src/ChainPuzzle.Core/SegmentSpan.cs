namespace ChainPuzzle.Core;

/// <summary>
/// Describes the index range of a single segment within the full chain point array.
/// </summary>
/// <param name="StartIndex">Inclusive start index in the chain point array.</param>
/// <param name="EndIndex">Inclusive end index in the chain point array.</param>
/// <param name="Length">Number of links in this segment.</param>
public readonly record struct SegmentSpan(int StartIndex, int EndIndex, int Length);
