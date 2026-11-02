namespace SportsPlatform.Modules.SportsConfiguration.Generators;

/// <summary>
/// The draws' random source (S05 D10): xoshiro256** (Blackman and Vigna, 2018), its state filled from the seed by
/// SplitMix64, with unbiased bounded draws (Lemire, 2019). The algorithm is the module's own, unlike
/// <c>System.Random</c>'s, so a stored seed gives the same draw on every machine and .NET version. Not for secrets.
/// </summary>
public sealed class SeededRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public SeededRandom(ulong seed)
    {
        var splitMix = seed;
        _s0 = SplitMix64(ref splitMix);
        _s1 = SplitMix64(ref splitMix);
        _s2 = SplitMix64(ref splitMix);
        _s3 = SplitMix64(ref splitMix);
    }

    private SeededRandom(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        if ((s0 | s1 | s2 | s3) == 0)
        {
            throw new ArgumentException("The state of xoshiro256** can't be all zero.");
        }

        (_s0, _s1, _s2, _s3) = (s0, s1, s2, s3);
    }

    /// <summary>A generator with the given state, to check the algorithm against its published outputs.</summary>
    public static SeededRandom FromState(ulong s0, ulong s1, ulong s2, ulong s3) => new(s0, s1, s2, s3);

    /// <summary>SplitMix64's next output, advancing <paramref name="state"/>.</summary>
    public static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>The next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>A whole number from 0 up to, not including, <paramref name="bound"/>; every value equally likely.</summary>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        var range = (ulong)bound;
        var high = Math.BigMul(NextUInt64(), range, out var low);
        if (low < range)
        {
            var threshold = (0UL - range) % range;
            while (low < threshold)
            {
                high = Math.BigMul(NextUInt64(), range, out low);
            }
        }

        return (int)high;
    }

    /// <summary><paramref name="items"/> in a random order (Fisher–Yates, from the end).</summary>
    public IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var shuffled = items.ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));
}
