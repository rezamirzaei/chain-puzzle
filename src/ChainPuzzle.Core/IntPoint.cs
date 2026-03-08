namespace ChainPuzzle.Core;

/// <summary>
/// An immutable 2D integer point used for hex-grid coordinates (axial representation).
/// </summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate (axial second axis).</param>
public readonly record struct IntPoint(int X, int Y)
{
    /// <summary>Returns the component-wise sum of two points.</summary>
    public static IntPoint operator +(IntPoint left, IntPoint right)
    {
        return new IntPoint(left.X + right.X, left.Y + right.Y);
    }

    /// <summary>Returns a compact string key suitable for dictionary lookups.</summary>
    public string Key()
    {
        return $"{X},{Y}";
    }
}
