namespace Gorm.Application.Temporal.Operations.Benchmarking;

/// <summary>
/// Contains latency, allocation, GC and determinism evidence.
/// </summary>
public sealed class GraphTwinBenchmarkResult
{
    /// <summary>
    /// Gets measured iterations.
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// Gets total measured duration.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Gets mean end-to-end microseconds per iteration.
    /// </summary>
    public required double MeanMicroseconds { get; init; }

    /// <summary>
    /// Gets minimum end-to-end microseconds.
    /// </summary>
    public required double MinimumMicroseconds { get; init; }

    /// <summary>
    /// Gets maximum end-to-end microseconds.
    /// </summary>
    public required double MaximumMicroseconds { get; init; }

    /// <summary>
    /// Gets operations per second.
    /// </summary>
    public required double OperationsPerSecond { get; init; }

    /// <summary>
    /// Gets bytes allocated on the invoking thread during measured iterations.
    /// </summary>
    public required long AllocatedBytes { get; init; }

    /// <summary>
    /// Gets generation-zero collections during measured iterations.
    /// </summary>
    public required int Generation0Collections { get; init; }

    /// <summary>
    /// Gets generation-one collections during measured iterations.
    /// </summary>
    public required int Generation1Collections { get; init; }

    /// <summary>
    /// Gets generation-two collections during measured iterations.
    /// </summary>
    public required int Generation2Collections { get; init; }

    /// <summary>
    /// Gets the stable last report fingerprint.
    /// </summary>
    public required string ReportFingerprint { get; init; }

    /// <summary>
    /// Gets whether every iteration produced the same report fingerprint.
    /// </summary>
    public required bool Deterministic { get; init; }

    /// <summary>
    /// Gets benchmark interpretation.
    /// </summary>
    public required string Explanation { get; init; }
}