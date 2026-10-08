using System.Diagnostics;
using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Operations.Reporting;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Operations.Benchmarking;

/// <summary>
/// Benchmarks exact drift plus canonical report construction on the invoking thread.
/// </summary>
public static class GraphTwinBenchmarkRunner
{
    /// <summary>
    /// Runs bounded warm-up and measured loops without adding a benchmark package.
    /// </summary>
    public static GraphTwinBenchmarkResult Run(
        GraphWorldSnapshot expected,
        GraphWorldSnapshot observed,
        GraphTwinBenchmarkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        options ??= new GraphTwinBenchmarkOptions();
        Validate(options);
        for (var index = 0; index < options.WarmupIterations; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Execute(expected, observed, options.DriftOptions, cancellationToken);
        }

        var durations = new double[options.Iterations];
        var beforeAllocations = GC.GetAllocatedBytesForCurrentThread();
        var beforeGen0 = GC.CollectionCount(0);
        var beforeGen1 = GC.CollectionCount(1);
        var beforeGen2 = GC.CollectionCount(2);
        var total = Stopwatch.StartNew();
        string? fingerprint = null;
        var deterministic = true;
        for (var index = 0; index < options.Iterations; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = Stopwatch.GetTimestamp();
            var report = Execute(expected, observed, options.DriftOptions, cancellationToken);
            durations[index] = Stopwatch.GetElapsedTime(started).TotalMicroseconds;
            fingerprint ??= report.ReportFingerprint;
            deterministic &= string.Equals(fingerprint, report.ReportFingerprint, StringComparison.Ordinal);
        }

        total.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeAllocations;
        var operationsPerSecond = options.Iterations / total.Elapsed.TotalSeconds;
        return new GraphTwinBenchmarkResult
        {
            Iterations = options.Iterations,
            Elapsed = total.Elapsed,
            MeanMicroseconds = durations.Average(),
            MinimumMicroseconds = durations.Min(),
            MaximumMicroseconds = durations.Max(),
            OperationsPerSecond = operationsPerSecond,
            AllocatedBytes = allocatedBytes,
            Generation0Collections = GC.CollectionCount(0) - beforeGen0,
            Generation1Collections = GC.CollectionCount(1) - beforeGen1,
            Generation2Collections = GC.CollectionCount(2) - beforeGen2,
            ReportFingerprint = fingerprint!,
            Deterministic = deterministic,
            Explanation =
                $"{options.Iterations} exact drift+report operations averaged {durations.Average():F3} µs and " +
                $"allocated {allocatedBytes} invoking-thread bytes; deterministic fingerprints: {deterministic}."
        };
    }

    private static GraphTwinEvidenceReport Execute(
        GraphWorldSnapshot expected,
        GraphWorldSnapshot observed,
        GraphTwinDriftOptions driftOptions,
        CancellationToken cancellationToken)
    {
        var drift = GraphTwinDriftDetector.Compare(expected, observed, driftOptions, cancellationToken);
        return GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "benchmark",
                CreatedAt = observed.Identity.RecordedAt,
                Synchronization = new GraphTwinSynchronizationResult
                {
                    Sequence = observed.Identity.Version,
                    ObservedAt = observed.Identity.RecordedAt,
                    Drift = drift
                },
                CancellationToken = cancellationToken
            });
    }

    private static void Validate(GraphTwinBenchmarkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.DriftOptions);
        if (options.WarmupIterations < 0 || options.Iterations <= 0 || options.MaximumIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if ((long)options.WarmupIterations + options.Iterations > options.MaximumIterations)
        {
            throw new InvalidOperationException(
                $"Benchmark warm-up plus measured iterations exceed MaximumIterations ({options.MaximumIterations}).");
        }
    }
}