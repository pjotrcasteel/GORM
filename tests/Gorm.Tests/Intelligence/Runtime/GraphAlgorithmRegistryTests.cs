using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;
using Gorm.Application.Intelligence.Studio;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Runtime;

[TestClass]
public sealed class GraphAlgorithmRegistryTests
{
    private static readonly GraphProjectionKey Key = new("registry:test");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SnapshotMetadata_IsStableAcrossProjectionInputOrder()
    {
        var first = Node(1);
        var second = Node(2);
        var edge = Edge(11, first, second);
        var createdAt = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var firstProjection = GraphProjection.Create([first, second], [edge]);
        var secondProjection = GraphProjection.Create([second, first], [edge]);
        var firstMetadata = GraphProjectionSnapshotMetadata.Create(Key, 4, firstProjection, createdAt);
        var secondMetadata = GraphProjectionSnapshotMetadata.Create(Key, 4, secondProjection, createdAt);

        Assert.AreEqual(firstMetadata.TopologyFingerprint, secondMetadata.TopologyFingerprint);
        Assert.AreEqual(2, firstMetadata.NodeCount);
        Assert.AreEqual(1, firstMetadata.EdgeCount);
        Assert.AreEqual(createdAt, firstMetadata.CreatedAt);
    }

    [TestMethod]
    public void ApplyDelta_PublishesParentVersionAndDeltaOrigin()
    {
        var first = Node(1);
        var snapshot = new GraphProjectionSnapshot(Key, 7, GraphProjection.Create([first], []));
        var update = snapshot.ApplyDelta(
            new GraphProjectionDeltaBuilder(Key, 7, 8).AddNodes([Node(2)]).Build(),
            TestContext.CancellationToken);

        Assert.AreEqual(7L, update.Snapshot.Metadata.ParentVersion);
        Assert.AreEqual("delta", update.Snapshot.Metadata.Origin);
        Assert.AreEqual(8, update.Snapshot.Metadata.Version);
        Assert.AreNotEqual(snapshot.Metadata.TopologyFingerprint, update.Snapshot.Metadata.TopologyFingerprint);
    }

    [TestMethod]
    public void Snapshot_WithMetadataForAnotherProjection_RejectsFalseProvenance()
    {
        var firstProjection = GraphProjection.Create([Node(1)], []);
        var secondProjection = GraphProjection.Create([Node(2)], []);
        var metadata = GraphProjectionSnapshotMetadata.Create(Key, 1, firstProjection);
        var rejected = false;

        try
        {
            _ = new GraphProjectionSnapshot(Key, 1, secondProjection, metadata);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Assert.IsTrue(rejected);
    }

    [TestMethod]
    public void BuiltInRegistry_ExposesStableCompleteCatalog()
    {
        var descriptors = GraphAlgorithmRegistry.CreateBuiltIn().Descriptors;

        Assert.HasCount(14, descriptors);
        CollectionAssert.AreEqual(
            descriptors.Select(descriptor => descriptor.Id).Order(StringComparer.Ordinal).ToArray(),
            descriptors.Select(descriptor => descriptor.Id).ToArray());
        var degree = descriptors.Single(descriptor => descriptor.Id == "centrality.degree");
        Assert.IsTrue(degree.SupportsIncremental);
        Assert.AreEqual(typeof(GraphDegreeCentralityOptions), degree.OptionsType);
        Assert.IsTrue(descriptors.All(descriptor => descriptor.SupportsLive));
    }

    [TestMethod]
    public void RegistryExecute_ReturnsTypedResultWithExactSnapshotMetadata()
    {
        var first = Node(1);
        var second = Node(2);
        var snapshot = new GraphProjectionSnapshot(Key, 3, GraphProjection.Create([first, second], [Edge(10, first, second)]));
        var invocationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var execution = GraphAlgorithmRegistry.CreateBuiltIn().Execute(
            snapshot,
            new GraphAlgorithmInvocation("centrality.degree", new GraphDegreeCentralityOptions(), invocationId: invocationId),
            TestContext.CancellationToken);

        Assert.AreEqual(invocationId, execution.InvocationId);
        Assert.AreEqual(snapshot.Metadata, execution.Snapshot);
        Assert.AreEqual(typeof(GraphDegreeCentralityResult), execution.ResultType);
        Assert.IsNotNull(execution.AsResult<GraphDegreeCentralityResult>());
        Assert.HasCount(2, execution.AsResult<GraphDegreeCentralityResult>()!.Scores);
    }

    [TestMethod]
    public void RegistryExecute_WithMissingRouteParameter_FailsBeforeExecution()
    {
        var snapshot = new GraphProjectionSnapshot(Key, 1, GraphProjection.Create([Node(1)], []));
        var rejected = false;

        try
        {
            GraphAlgorithmRegistry.CreateBuiltIn().Execute(
                snapshot,
                new GraphAlgorithmInvocation("routing.shortest-path"),
                TestContext.CancellationToken);
        }
        catch (ArgumentException exception)
        {
            rejected = exception.Message.Contains("startNodeId", StringComparison.Ordinal);
        }

        Assert.IsTrue(rejected);
    }

    [TestMethod]
    public void Registry_CustomRegistration_RejectsDuplicateIdentifiers()
    {
        var descriptor = new GraphAlgorithmDescriptor(
            new GraphAlgorithmDescriptor.GraphAlgorithmDescriptorParameters
            {
                Id = "custom.count",
                DisplayName = "Count",
                Description = "Counts projected nodes.",
                Category = GraphAlgorithmCategory.Custom,
                ResultType = typeof(int)
            });
        var registry = new GraphAlgorithmRegistry().Register(descriptor, (projection, _, _) => projection.Nodes.Count);
        var rejected = false;

        try
        {
            registry.Register(descriptor, (projection, _, _) => projection.Edges.Count);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Assert.IsTrue(rejected);
    }

    [TestMethod]
    public void StudioBridge_ProducesSerializableEditorMetadataAndProjectionSummary()
    {
        var bridge = new GraphStudioIntelligenceBridge(GraphAlgorithmRegistry.CreateBuiltIn());
        var catalog = bridge.Catalog;
        var pageRank = catalog.Algorithms.Single(algorithm => algorithm.Id == "centrality.pagerank");
        var progress = pageRank.Options.Single(option => option.Name == "Progress");
        var damping = pageRank.Options.Single(option => option.Name == "DampingFactor");
        var snapshot = new GraphProjectionSnapshot(Key, 9, GraphProjection.Create([Node(1)], []));
        var summary = GraphStudioIntelligenceBridge.Describe(snapshot);

        Assert.AreEqual(1, catalog.SchemaVersion);
        Assert.IsFalse(progress.IsEditable);
        Assert.AreEqual(GraphStudioValueKind.Custom, progress.Kind);
        Assert.IsTrue(damping.IsEditable);
        Assert.AreEqual("0.85", damping.DefaultValue);
        Assert.AreEqual(Key.Value, summary.Key);
        Assert.AreEqual(snapshot.Metadata.TopologyFingerprint, summary.TopologyFingerprint);
    }

    private static RegistryNode Node(int identifier) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}")
    };

    private static RegistryEdge Edge(int identifier, RegistryNode from, RegistryNode to) => new()
    {
        Id = Guid.Parse($"10000000-0000-0000-0000-{identifier:D12}"),
        FromId = from.Id,
        ToId = to.Id
    };

    private sealed class RegistryNode : Node;

    private sealed class RegistryEdge : Edge;
}