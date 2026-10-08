using System.Buffers.Binary;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Output;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;
using Gorm.Application.Intelligence.Runtime;

namespace Gorm.Application.Intelligence.Benchmarking;

internal static class GraphIntelligenceSoakSetupFactory
{
    public static async Task<GraphIntelligenceSoakSetup> CreateAsync(GraphProjectionKey key, GraphIntelligenceSoakOptions options, CancellationToken cancellationToken)
    {
        var nodes = Enumerable.Range(0, options.NodeCount).Select(index => CreateNode(index, revision: 0)).ToArray();
        var edges = Enumerable.Range(0, options.NodeCount).Select(index => CreateEdge(index, nodes[index], nodes[(index + 1) % nodes.Length])).ToArray();
        var initial = new GraphProjectionSnapshot(key, 1, GraphProjection.Create(nodes, edges));
        var cache = new GraphProjectionCache(new GraphProjectionCacheOptions { MaximumEntries = 1, TimeToLive = TimeSpan.FromHours(1) });
        await cache.GetOrCreateAsync(key, 1, _ => Task.FromResult(initial), cancellationToken).ConfigureAwait(false);

        var runtime = new GraphIntelligenceRuntime(
            GraphAlgorithmRegistry.CreateBuiltIn(),
            cache,
            new GraphIntelligenceRuntimeOptions
            {
                ExecutionTimeout = options.ExecutionTimeout,
                MaximumLiveExecutions = options.Iterations
            });
        var outputCounter = new GraphIntelligenceSoakOutputCounter();
        var dispatcher = CreateDispatcher(outputCounter);
        return new GraphIntelligenceSoakSetup
        {
            Nodes = nodes,
            InitialFingerprint = initial.Metadata.TopologyFingerprint,
            Cache = cache,
            Runtime = runtime,
            Dispatcher = dispatcher,
            Feed = new GraphChannelProjectionChangeFeed(options.FeedCapacity),
            OutputCounter = outputCounter
        };
    }

    public static GraphIntelligenceSoakNode CreateNode(int index, int revision) =>
        new()
        {
            Id = CreateIdentifier(0x10, index),
            Revision = revision
        };

    private static GraphIntelligenceOutputDispatcher CreateDispatcher(GraphIntelligenceSoakOutputCounter outputCounter) =>
        new(
        [
            new GraphDelegateIntelligenceOutputAdapter(
                "soak-counter",
                (_, _) =>
                {
                    Interlocked.Increment(ref outputCounter.Value);
                    return ValueTask.CompletedTask;
                })
        ]);

    private static GraphIntelligenceSoakEdge CreateEdge(int index, GraphIntelligenceSoakNode from, GraphIntelligenceSoakNode to) =>
        new()
        {
            Id = CreateIdentifier(0x20, index),
            FromId = from.Id,
            ToId = to.Id
        };

    private static Guid CreateIdentifier(byte prefix, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = prefix;
        BinaryPrimitives.WriteInt32LittleEndian(bytes[12..], index);
        return new Guid(bytes);
    }
}