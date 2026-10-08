namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

internal sealed class GraphDeterministicRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E37_79B9_7F4A_7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D0_49BB_1331_11EBUL;
        return value ^ (value >> 31);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1d / 9_007_199_254_740_992d);

    public static ulong DeriveSeed(ulong experimentSeed, int runIndex)
    {
        var value = experimentSeed + (((ulong)runIndex + 1) * 0x9E37_79B9_7F4A_7C15UL);
        value = (value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D0_49BB_1331_11EBUL;
        return value ^ (value >> 31);
    }
}