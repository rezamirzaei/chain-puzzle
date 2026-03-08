namespace ChainPuzzle.Core;

/// <summary>
/// A deterministic pseudo-random number generator seeded by a string.
/// Uses a linear congruential generator (LCG) for reproducible sequences.
/// </summary>
public sealed class SeededRandom
{
    private uint _state;

    public SeededRandom(string seed)
    {
        if (seed is null)
        {
            throw new ArgumentNullException(nameof(seed));
        }

        _state = Normalize(seed);
        if (_state == 0)
        {
            _state = 123_456_789;
        }
    }

    public double Next()
    {
        _state = unchecked((uint)((1_664_525 * _state) + 1_013_904_223));
        return _state / ((double)uint.MaxValue + 1d);
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        return (int)Math.Floor(Next() * maxExclusive);
    }

    private static uint Normalize(string seed)
    {
        var hash = 2_166_136_261u;
        foreach (var character in seed)
        {
            hash ^= character;
            hash = unchecked(hash * 16_777_619u);
        }

        return hash;
    }
}
