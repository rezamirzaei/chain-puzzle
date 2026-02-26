namespace ChainPuzzle.Core;

public readonly record struct IntPoint(int X, int Y)
{
    public static IntPoint operator +(IntPoint left, IntPoint right)
    {
        return new IntPoint(left.X + right.X, left.Y + right.Y);
    }

    public string Key()
    {
        return $"{X},{Y}";
    }
}
