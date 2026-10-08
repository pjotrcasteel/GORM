namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Represents a bounded directed-cycle search result.
/// </summary>
public sealed class GraphCycleDetectionResult
{
    internal GraphCycleDetectionResult(IReadOnlyList<GraphCycle> cycles, bool truncated)
    {
        Cycles = cycles;
        Truncated = truncated;
    }

    /// <summary>
    /// Gets the detected simple cycles.
    /// </summary>
    public IReadOnlyList<GraphCycle> Cycles { get; }

    /// <summary>
    /// Gets a value indicating whether the maximum cycle count or search depth bounded the result.
    /// </summary>
    public bool Truncated { get; }

    /// <summary>
    /// Gets a value indicating whether at least one cycle was found.
    /// </summary>
    public bool HasCycles =>
        Cycles.Count > 0;
}