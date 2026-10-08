using System.Diagnostics;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Exercises bounded live projection, registry execution and explicit output without adding a benchmark dependency.
/// </summary>
public static class GraphIntelligenceSoakRunner
{
    private static readonly GraphProjectionKey SoakKey = new("benchmark:live-soak");

    /// <summary>
    /// Runs a deterministic live degree-centrality soak profile.
    /// </summary>
    public static async Task<GraphIntelligenceSoakResult> RunAsync(GraphIntelligenceSoakOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new GraphIntelligenceSoakOptions();
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();

        var baseline = CaptureMetrics();
        var setup = await GraphIntelligenceSoakSetupFactory.CreateAsync(SoakKey, options, cancellationToken).ConfigureAwait(false);
        var execution = await RunUpdatesAsync(setup, options, cancellationToken).ConfigureAwait(false);
        var final = ValidateCompletion(setup, execution, options);
        return CreateResult(setup, execution, final, baseline);
    }

    private static GraphIntelligenceSoakMetricsBaseline CaptureMetrics() =>
        new(
            GC.GetTotalAllocatedBytes(precise: false),
            GC.GetTotalMemory(forceFullCollection: false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            Stopwatch.GetTimestamp());

    private static async Task<GraphIntelligenceSoakExecution> RunUpdatesAsync(
        GraphIntelligenceSoakSetup setup,
        GraphIntelligenceSoakOptions options,
        CancellationToken cancellationToken)
    {
        using var producerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producer = PublishChangesAsync(setup.Feed, setup.Nodes, options.Iterations, producerCancellation.Token);
        var completed = 0;
        GraphDegreeCentralityResult? finalDegree = null;

        try
        {
            await foreach (var update in setup.Runtime.WatchAsync(
                SoakKey,
                setup.Feed,
                _ => throw new InvalidOperationException("A compatible soak delta must not rebuild."),
                _ => new GraphAlgorithmInvocation("centrality.degree"),
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                completed++;
                finalDegree = update.Execution.AsResult<GraphDegreeCentralityResult>();
                if (options.OutputDispatchInterval > 0 && completed % options.OutputDispatchInterval == 0)
                {
                    await setup.Dispatcher.DispatchAsync(update.Execution, cancellationToken).ConfigureAwait(false);
                }
            }

            await producer.ConfigureAwait(false);
        }
        catch
        {
            await producerCancellation.CancelAsync().ConfigureAwait(false);
            setup.Feed.Complete(SoakKey);
            try
            {
                await producer.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Preserve the original consumer/algorithm exception.
            }

            throw;
        }

        return new GraphIntelligenceSoakExecution(completed, finalDegree);
    }

    private static GraphProjectionSnapshot ValidateCompletion(GraphIntelligenceSoakSetup setup, GraphIntelligenceSoakExecution execution, GraphIntelligenceSoakOptions options)
    {
        if (execution.CompletedIterations != options.Iterations ||
            execution.FinalDegree is null ||
            !setup.Cache.TryGet(SoakKey, 1 + options.Iterations, out var final) ||
            final is null)
        {
            throw new InvalidOperationException("The soak run did not publish its exact expected final version and result.");
        }

        if (final.Metadata.TopologyFingerprint != setup.InitialFingerprint)
        {
            throw new InvalidOperationException("Property-only soak updates changed the deterministic topology fingerprint.");
        }

        return final;
    }

    private static GraphIntelligenceSoakResult CreateResult(
        GraphIntelligenceSoakSetup setup,
        GraphIntelligenceSoakExecution execution,
        GraphProjectionSnapshot final,
        GraphIntelligenceSoakMetricsBaseline baseline)
    {
        var statistics = setup.Cache.Statistics;
        return new GraphIntelligenceSoakResult
        {
            CompletedIterations = execution.CompletedIterations,
            OutputDispatchCount = setup.OutputCounter.Value,
            Duration = Stopwatch.GetElapsedTime(baseline.StartedAt),
            AllocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - baseline.AllocatedBytes),
            ManagedMemoryDeltaBytes = GC.GetTotalMemory(forceFullCollection: false) - baseline.ManagedMemoryBytes,
            Generation0Collections = GC.CollectionCount(0) - baseline.Generation0Collections,
            Generation1Collections = GC.CollectionCount(1) - baseline.Generation1Collections,
            Generation2Collections = GC.CollectionCount(2) - baseline.Generation2Collections,
            FinalVersion = final.Version,
            FinalTopologyFingerprint = final.Metadata.TopologyFingerprint,
            FinalDegreeChecksum = CalculateDegreeChecksum(execution.FinalDegree!),
            IncrementalUpdates = statistics.IncrementalUpdates,
            FullRebuilds = statistics.FullRebuilds
        };
    }

    private static async Task PublishChangesAsync(GraphChannelProjectionChangeFeed feed, GraphIntelligenceSoakNode[] nodes, int iterations, CancellationToken cancellationToken)
    {
        try
        {
            for (var iteration = 1; iteration <= iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nodeIndex = (iteration - 1) % nodes.Length;
                var updatedNode = GraphIntelligenceSoakSetupFactory.CreateNode(nodeIndex, iteration);
                var delta = new GraphProjectionDeltaBuilder(SoakKey, iteration, iteration + 1).UpdateNodes([updatedNode]).Build();
                await feed.PublishAsync(new GraphProjectionChange(iteration, delta), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            feed.Complete(SoakKey);
        }
    }

    private static long CalculateDegreeChecksum(GraphDegreeCentralityResult result)
    {
        long checksum = 17;
        foreach (var score in result.Scores.OrderBy(score => score.Node.Id))
        {
            checksum = unchecked((checksum * 31) + score.IncomingDegree);
            checksum = unchecked((checksum * 31) + score.OutgoingDegree);
        }

        return checksum;
    }

    private static void ValidateOptions(GraphIntelligenceSoakOptions options)
    {
        if (options.NodeCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "A soak graph needs at least two nodes.");
        }

        if (options.Iterations <= 0 || options.Iterations == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Soak iterations must be greater than zero and leave room for the final version.");
        }

        if (options.FeedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Feed capacity must be greater than zero.");
        }

        if (options.OutputDispatchInterval < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Output dispatch interval cannot be negative.");
        }

        if (options.ExecutionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Execution timeout must be greater than zero.");
        }
    }
}