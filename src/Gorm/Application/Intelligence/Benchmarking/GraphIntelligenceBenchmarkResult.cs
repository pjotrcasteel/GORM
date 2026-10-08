namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Represents an in-process graph intelligence benchmark baseline.
/// </summary>
public sealed class GraphIntelligenceBenchmarkResult
{
    /// <summary>
    /// Gets the executed graph-size profile.
    /// </summary>
    public required GraphIntelligenceBenchmarkProfile Profile { get; init; }

    /// <summary>
    /// Gets measurements in stable operation order.
    /// </summary>
    public required IReadOnlyList<GraphIntelligenceBenchmarkMeasurement> Measurements { get; init; }

    /// <summary>
    /// Gets the runtime framework description for comparison context.
    /// </summary>
    public required string FrameworkDescription { get; init; }

    /// <summary>
    /// Gets whether the process uses a server garbage collector.
    /// </summary>
    public required bool IsServerGarbageCollector { get; init; }
}