using System.Text.Json;
using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Benchmarking;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;
using Gorm.Application.Intelligence.Studio;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Runtime;

[TestClass]
public sealed class GraphIntelligenceStabilityTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PreLiveProjectionAndAlgorithmSurface_RemainsSourceCompatible()
    {
        var first = Node(1);
        var second = Node(2);
        var projection = GraphProjection.Create([first, second], [Edge(10, first, second)]);
        var key = new GraphProjectionKey("compatibility:legacy");
        var snapshot = new GraphProjectionSnapshot(key, 1, projection);
        var cache = new GraphProjectionCache();
        IGraphAlgorithm<GraphPageRankResult> pageRankAlgorithm = new GraphPageRankAlgorithm();

        var pageRank = projection.Run(pageRankAlgorithm, TestContext.CancellationToken);
        var degree = projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);
        var components = projection.ConnectedComponents(cancellationToken: TestContext.CancellationToken);
        var path = projection.ShortestPath(first.Id, second.Id, cancellationToken: TestContext.CancellationToken);

        Assert.IsNotNull(pageRank);
        Assert.HasCount(2, degree.Scores);
        Assert.HasCount(1, components.Components);
        Assert.IsNotNull(path);
        Assert.AreEqual(1, snapshot.Version);
        Assert.AreEqual(0, cache.Count);
    }

    [TestMethod]
    public void StableConstructorsAndExtensionSignatures_AreStillDiscoverable()
    {
        var snapshotConstructor = typeof(GraphProjectionSnapshot).GetConstructor([typeof(GraphProjectionKey), typeof(long), typeof(GraphProjection)]);
        var pageRank = typeof(GraphIntelligenceExtensions).GetMethod(
            nameof(GraphIntelligenceExtensions.PageRank),
            [typeof(GraphProjection), typeof(GraphPageRankOptions), typeof(CancellationToken)]);
        var projectSnapshot = typeof(GraphContextIntelligenceExtensions).GetMethods()
            .Single(method => method.Name == nameof(GraphContextIntelligenceExtensions.ProjectGraphSnapshotAsync));

        Assert.IsNotNull(snapshotConstructor);
        Assert.IsNotNull(pageRank);
        Assert.IsNotNull(projectSnapshot);
    }

    [TestMethod]
    public void BuiltInAlgorithmIdentifiers_MatchStableApiBaseline()
    {
        var actual = GraphAlgorithmRegistry.CreateBuiltIn().Descriptors.Select(descriptor => descriptor.Id).ToArray();
        string[] expected =
        [
            "centrality.betweenness",
            "centrality.degree",
            "centrality.pagerank",
            "community.leiden",
            "flow.maximum",
            "planning.critical-path",
            "prediction.links",
            "routing.astar",
            "routing.impact",
            "routing.k-shortest",
            "routing.pareto",
            "routing.shortest-path",
            "structure.connected-components",
            "structure.cycles"
        ];

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void StudioCatalogAndProjectionSummary_AreJsonSerializableDtos()
    {
        var bridge = new GraphStudioIntelligenceBridge(GraphAlgorithmRegistry.CreateBuiltIn());
        var catalogJson = JsonSerializer.Serialize(bridge.Catalog);
        var snapshot = new GraphProjectionSnapshot(new GraphProjectionKey("compatibility:json"), 3, GraphProjection.Create([Node(1)], []));
        var summaryJson = JsonSerializer.Serialize(GraphStudioIntelligenceBridge.Describe(snapshot));

        Assert.Contains("centrality.pagerank", catalogJson);
        Assert.Contains("DampingFactor", catalogJson);
        Assert.Contains("TopologyFingerprint", summaryJson);
        Assert.Contains("compatibility:json", summaryJson);
    }

    [TestMethod]
    public async Task SoakRunner_CompletesBoundedLiveRunWithoutRebuildAndWithStableIdentity()
    {
        var options = new GraphIntelligenceSoakOptions
        {
            NodeCount = 16,
            Iterations = 25,
            FeedCapacity = 3,
            OutputDispatchInterval = 5,
            ExecutionTimeout = TimeSpan.FromSeconds(10)
        };

        var first = await GraphIntelligenceSoakRunner.RunAsync(options, TestContext.CancellationToken);
        var second = await GraphIntelligenceSoakRunner.RunAsync(options, TestContext.CancellationToken);

        Assert.AreEqual(25, first.CompletedIterations);
        Assert.AreEqual(5, first.OutputDispatchCount);
        Assert.AreEqual(26, first.FinalVersion);
        Assert.AreEqual(25, first.IncrementalUpdates);
        Assert.AreEqual(0, first.FullRebuilds);
        Assert.AreEqual(first.FinalTopologyFingerprint, second.FinalTopologyFingerprint);
        Assert.AreEqual(first.FinalDegreeChecksum, second.FinalDegreeChecksum);
    }

    [TestMethod]
    public async Task SoakRunner_WithCallerCancellation_StopsImmediately()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = false;

        try
        {
            await GraphIntelligenceSoakRunner.RunAsync(
                new GraphIntelligenceSoakOptions { Iterations = 10 },
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.IsTrue(cancelled);
    }

    private static CompatibilityNode Node(int identifier) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}")
    };

    private static CompatibilityEdge Edge(int identifier, CompatibilityNode from, CompatibilityNode to) => new()
    {
        Id = Guid.Parse($"10000000-0000-0000-0000-{identifier:D12}"),
        FromId = from.Id,
        ToId = to.Id
    };

    private sealed class CompatibilityNode : Node;

    private sealed class CompatibilityEdge : Edge;
}