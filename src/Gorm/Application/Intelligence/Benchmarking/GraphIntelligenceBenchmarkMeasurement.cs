namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Represents stable summary statistics for one benchmark operation.
/// </summary>
public sealed class GraphIntelligenceBenchmarkMeasurement
{
    /// <summary>
    /// Gets the stable operation name.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the measured iteration count.
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Gets the median elapsed milliseconds.
    /// </summary>
    public required double MedianMilliseconds { get; init; }

    /// <summary>
    /// Gets the minimum elapsed milliseconds.
    /// </summary>
    public required double MinimumMilliseconds { get; init; }

    /// <summary>
    /// Gets the maximum elapsed milliseconds.
    /// </summary>
    public required double MaximumMilliseconds { get; init; }

    /// <summary>
    /// Gets average allocations on the invoking thread.
    /// </summary>
    public required long AverageAllocatedBytes { get; init; }

    /// <summary>
    /// Gets median operations per second.
    /// </summary>
    public double OperationsPerSecond =>
        MedianMilliseconds <= 0 ? double.PositiveInfinity : 1_000d / MedianMilliseconds;
}