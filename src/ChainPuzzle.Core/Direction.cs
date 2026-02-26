namespace ChainPuzzle.Core;

public enum Direction
{
    East = 0,
    NorthEast = 1,
    NorthWest = 2,
    West = 3,
    SouthWest = 4,
    SouthEast = 5
}

public static class DirectionExtensions
{
    public static IntPoint ToVector(this Direction direction)
    {
        return direction switch
        {
            Direction.East => new IntPoint(1, 0),
            Direction.NorthEast => new IntPoint(1, -1),
            Direction.NorthWest => new IntPoint(0, -1),
            Direction.West => new IntPoint(-1, 0),
            Direction.SouthWest => new IntPoint(-1, 1),
            Direction.SouthEast => new IntPoint(0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported direction.")
        };
    }

    public static Direction Rotate(this Direction direction, int steps)
    {
        var next = ((int)direction + steps) % 6;
        if (next < 0)
        {
            next += 6;
        }

        return (Direction)next;
    }

    public static Direction Parse(char letter)
    {
        return char.ToUpperInvariant(letter) switch
        {
            'E' => Direction.East,
            'W' => Direction.West,
            _ => throw new ArgumentException($"Invalid direction letter: {letter}", nameof(letter))
        };
    }

    public static Direction ParseToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Direction token must not be empty.", nameof(token));
        }

        return token.Trim().ToUpperInvariant() switch
        {
            "E" => Direction.East,
            "W" => Direction.West,
            "NE" => Direction.NorthEast,
            "NW" => Direction.NorthWest,
            "SE" => Direction.SouthEast,
            "SW" => Direction.SouthWest,
            _ => throw new ArgumentException($"Invalid direction token: {token}", nameof(token))
        };
    }

    public static char ToLetter(this Direction direction)
    {
        return direction switch
        {
            Direction.East => 'E',
            Direction.West => 'W',
            Direction.NorthEast => 'A',
            Direction.NorthWest => 'B',
            Direction.SouthEast => 'C',
            Direction.SouthWest => 'D',
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported direction.")
        };
    }
}
