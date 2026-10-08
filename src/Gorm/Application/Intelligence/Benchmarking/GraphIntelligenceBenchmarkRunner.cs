using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Runs dependency-free, reproducible projection and algorithm baselines for standard graph sizes.
/// </summary>
public sealed class GraphIntelligenceBenchmarkRunner
{
    /// <summary>
    /// Executes projection-build, degree-centrality and bounded PageRank baselines.
    /// Compiler note: this cannot be static.
    /// </summary>
    public static Task<GraphIntelligenceBenchmarkResult> RunAsync(
        GraphIntelligenceBenchmarkProfile profile,
        GraphIntelligenceBenchmarkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        options ??= new GraphIntelligenceBenchmarkOptions();
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();
        return RunCoreAsync(profile, options, cancellationToken);
    }

    private static async Task<GraphIntelligenceBenchmarkResult> RunCoreAsync(
        GraphIntelligenceBenchmarkProfile profile,
        GraphIntelligenceBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        var nodes = CreateNodes(profile.NodeCount);
        var edges = CreateEdges(nodes, profile.EdgeCount);
        var projection = GraphProjection.Create(nodes, edges);
        var key = new GraphProjectionKey($"benchmark:{profile.Name}");
        var snapshot = new GraphProjectionSnapshot(key, 1, projection);
        var cache = new GraphProjectionCache(new GraphProjectionCacheOptions { TimeToLive = Timeout.InfiniteTimeSpan, MaximumEntries = 1 });

        await cache.GetOrCreateAsync(key, 1, _ => Task.FromResult(snapshot), cancellationToken).ConfigureAwait(false);

        var previousDegree = snapshot.DegreeCentralityVersioned(cancellationToken: cancellationToken);
        var projectionUpdate = snapshot.ApplyDelta(new GraphProjectionDeltaBuilder(key, 1, 2).Build(), cancellationToken);
        var measurements = new List<GraphIntelligenceBenchmarkMeasurement>(5)
        {
            Measure("projection.build", () => GraphProjection.Create(nodes, edges), options, cancellationToken),
            Measure(
                "projection.cache-hit",
                () => cache.TryGet(key, 1, out var cached)
                    ? cached
                    : throw new InvalidOperationException("Benchmark cache hit must not load."),
                options,
                cancellationToken),
            Measure("degree-centrality.full", () => projection.DegreeCentrality(cancellationToken), options, cancellationToken),
            Measure(
                "degree-centrality.incremental-noop",
                () => projectionUpdate.UpdateDegreeCentrality(previousDegree, cancellationToken: cancellationToken),
                options,
                cancellationToken),
            Measure("pagerank", () => projection.PageRank(new GraphPageRankOptions
                {
                    MaximumIterations = options.PageRankIterations,
                    Tolerance = double.Epsilon
                },
                cancellationToken),
                options,
                cancellationToken)
        };

        return new GraphIntelligenceBenchmarkResult
        {
            Profile = profile,
            Measurements = measurements,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            IsServerGarbageCollector = GCSettings.IsServerGC
        };
    }

    private static GraphIntelligenceBenchmarkMeasurement Measure<TResult>(
        string operation,
        Func<TResult> execute,
        GraphIntelligenceBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        for (var iteration = 0; iteration < options.WarmupIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GC.KeepAlive(execute());
        }

        var elapsed = new double[options.MeasuredIterations];
        var allocated = new long[options.MeasuredIterations];
        for (var iteration = 0; iteration < options.MeasuredIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var startedAt = Stopwatch.GetTimestamp();
            var result = execute();
            elapsed[iteration] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            allocated[iteration] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            GC.KeepAlive(result);
        }

        options.Progress?.Report(new GraphAlgorithmProgress
        {
            Operation = "benchmark",
            Stage = operation,
            Completed = options.MeasuredIterations,
            Total = options.MeasuredIterations,
            Message = $"Completed {options.MeasuredIterations} measured '{operation}' benchmark iteration(s)."
        });

        var orderedElapsed = elapsed.Order().ToArray();
        var middle = orderedElapsed.Length / 2;
        var median = orderedElapsed.Length % 2 == 0 ? (orderedElapsed[middle - 1] + orderedElapsed[middle]) / 2 : orderedElapsed[middle];

        return new GraphIntelligenceBenchmarkMeasurement
        {
            Operation = operation,
            Iterations = options.MeasuredIterations,
            MedianMilliseconds = median,
            MinimumMilliseconds = orderedElapsed[0],
            MaximumMilliseconds = orderedElapsed[^1],
            AverageAllocatedBytes = (long)allocated.Average()
        };
    }

    private static BenchmarkNode[] CreateNodes(int nodeCount)
    {
        var nodes = new BenchmarkNode[nodeCount];
        for (var index = 0; index < nodeCount; index++)
        {
            nodes[index] = new BenchmarkNode { Id = CreateIdentifier(1, index) };
        }

        return nodes;
    }

    private static BenchmarkEdge[] CreateEdges(BenchmarkNode[] nodes, int edgeCount)
    {
        var edges = new BenchmarkEdge[edgeCount];
        for (var edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
        {
            var fromIndex = edgeIndex % nodes.Length;
            var stride = 1 + (edgeIndex / nodes.Length % Math.Max(nodes.Length - 1, 1));
            var toIndex = (fromIndex + stride) % nodes.Length;
            edges[edgeIndex] = new BenchmarkEdge
            {
                Id = CreateIdentifier(2, edgeIndex),
                FromId = nodes[fromIndex].Id,
                ToId = nodes[toIndex].Id
            };
        }

        return edges;
    }

    private static Guid CreateIdentifier(short kind, int index) =>
        new(index + 1, kind, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static void ValidateOptions(GraphIntelligenceBenchmarkOptions options)
    {
        if (options.WarmupIterations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Warmup iterations cannot be negative.");
        }

        if (options.MeasuredIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Measured iterations must be greater than zero.");
        }

        if (options.PageRankIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "PageRank iterations must be greater than zero.");
        }
    }

    private sealed class BenchmarkNode : Node;

    private sealed class BenchmarkEdge : Edge;
}