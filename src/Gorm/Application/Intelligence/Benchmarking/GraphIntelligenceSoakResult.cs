namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Contains correctness, duration, allocation and GC evidence from one live soak run.
/// </summary>
public sealed class GraphIntelligenceSoakResult
{
    /// <summary>
    /// Gets the number of completed live executions.
    /// </summary>
    public required int CompletedIterations { get; init; }

    /// <summary>
    /// Gets the number of explicitly dispatched output results.
    /// </summary>
    public required int OutputDispatchCount { get; init; }

    /// <summary>
    /// Gets elapsed wall-clock time.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets approximate process-wide bytes allocated during the run.
    /// </summary>
    public required long AllocatedBytes { get; init; }

    /// <summary>
    /// Gets the signed managed-memory change without forcing collection.
    /// </summary>
    public required long ManagedMemoryDeltaBytes { get; init; }

    /// <summary>
    /// Gets generation-zero collections observed during the run.
    /// </summary>
    public required int Generation0Collections { get; init; }

    /// <summary>
    /// Gets generation-one collections observed during the run.
    /// </summary>
    public required int Generation1Collections { get; init; }

    /// <summary>
    /// Gets generation-two collections observed during the run.
    /// </summary>
    public required int Generation2Collections { get; init; }

    /// <summary>
    /// Gets the final source version.
    /// </summary>
    public required long FinalVersion { get; init; }

    /// <summary>
    /// Gets the final deterministic topology fingerprint.
    /// </summary>
    public required string FinalTopologyFingerprint { get; init; }

    /// <summary>
    /// Gets the deterministic final degree checksum.
    /// </summary>
    public required long FinalDegreeChecksum { get; init; }

    /// <summary>
    /// Gets the number of incremental cache publications.
    /// </summary>
    public required long IncrementalUpdates { get; init; }

    /// <summary>
    /// Gets the number of full rebuilds performed during the run.
    /// </summary>
    public required long FullRebuilds { get; init; }
}