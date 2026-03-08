namespace ChainPuzzle.Core;

/// <summary>
/// The six hex-grid movement directions (pointy-top orientation).
/// </summary>
public enum Direction
{
    /// <summary>East (+1, 0).</summary>
    East = 0,
    /// <summary>North-east (+1, −1).</summary>
    NorthEast = 1,
    /// <summary>North-west (0, −1).</summary>
    NorthWest = 2,
    /// <summary>West (−1, 0).</summary>
    West = 3,
    /// <summary>South-west (−1, +1).</summary>
    SouthWest = 4,
    /// <summary>South-east (0, +1).</summary>
    SouthEast = 5
}

/// <summary>Extension methods for <see cref="Direction"/>.</summary>
public static class DirectionExtensions
{
    /// <summary>Returns the axial unit vector for this direction.</summary>
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

    /// <summary>Rotates the direction by the given number of 60° steps.</summary>
    public static Direction Rotate(this Direction direction, int steps)
    {
        var next = ((int)direction + steps) % 6;
        if (next < 0)
        {
            next += 6;
        }

        return (Direction)next;
    }

    /// <summary>Parses a single character ('E' or 'W') into a direction.</summary>
    public static Direction Parse(char letter)
    {
        return char.ToUpperInvariant(letter) switch
        {
            'E' => Direction.East,
            'W' => Direction.West,
            'A' => Direction.NorthEast,
            'B' => Direction.NorthWest,
            'C' => Direction.SouthEast,
            'D' => Direction.SouthWest,
            _ => throw new ArgumentException($"Invalid direction letter: {letter}", nameof(letter))
        };
    }

    /// <summary>Parses a direction token such as "NE", "SW", "E", etc.</summary>
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
            "A" => Direction.NorthEast,
            "B" => Direction.NorthWest,
            "C" => Direction.SouthEast,
            "D" => Direction.SouthWest,
            _ => throw new ArgumentException($"Invalid direction token: {token}", nameof(token))
        };
    }

    /// <summary>Returns a single-character representation used for serialisation.</summary>
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
