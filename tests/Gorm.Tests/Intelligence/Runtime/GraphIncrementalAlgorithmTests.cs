using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Execution;
using Gorm.Application.Intelligence.Incremental;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Runtime;

[TestClass]
public sealed class GraphIncrementalAlgorithmTests
{
    private static readonly GraphProjectionKey Key = new("incremental-algorithms");
    private static readonly int[] ExpectedPartitionIndexes = [0, 1, 2];
    private static readonly int[] ExpectedPartitionLengths = [4, 3, 3];
    private static readonly int[] ExpectedPartitionStartIndexes = [0, 4, 7];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void DeterministicPartitioner_CreatesBalancedContiguousPlan()
    {
        var partitions = GraphDeterministicPartitioner.Create(10, 3);

        CollectionAssert.AreEqual(ExpectedPartitionStartIndexes, partitions.Select(partition => partition.StartIndex).ToArray());
        CollectionAssert.AreEqual(ExpectedPartitionLengths, partitions.Select(partition => partition.Length).ToArray());
        CollectionAssert.AreEqual(ExpectedPartitionIndexes, partitions.Select(partition => partition.Index).ToArray());
    }

    [TestMethod]
    public void DeterministicPartitioner_RespectsMinimumPartitionSize()
    {
        var partitions = GraphDeterministicPartitioner.Create(100, 16, minimumItemsPerPartition: 30);

        Assert.HasCount(4, partitions);
        Assert.AreEqual(100, partitions.Sum(partition => partition.Length));
    }

    [TestMethod]
    public void PageRank_WithParallelPartitions_MatchesSequentialResultExactly()
    {
        var graph = CreateGraph();
        var sequential = graph.Projection.PageRank(new GraphPageRankOptions
        {
            DegreeOfParallelism = 1,
            MinimumNodesPerPartition = 1
        }, TestContext.CancellationToken);
        var parallel = graph.Projection.PageRank(new GraphPageRankOptions
        {
            DegreeOfParallelism = 4,
            MinimumNodesPerPartition = 1
        }, TestContext.CancellationToken);

        Assert.AreEqual(sequential.Iterations, parallel.Iterations);
        Assert.AreEqual(sequential.TotalDelta, parallel.TotalDelta);
        foreach (var node in graph.Projection.Nodes)
        {
            Assert.AreEqual(sequential.GetScore(node.Id), parallel.GetScore(node.Id));
        }
    }

    [TestMethod]
    public void DegreeCentrality_WithParallelPartitions_MatchesSequentialResultExactly()
    {
        var graph = CreateGraph();
        var sequential = graph.Projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);
        var parallel = graph.Projection.DegreeCentrality(new GraphDegreeCentralityOptions
        {
            DegreeOfParallelism = 4,
            MinimumNodesPerPartition = 1
        }, TestContext.CancellationToken);

        foreach (var expected in sequential.Scores)
        {
            var actual = parallel.Scores.Single(score => score.Node.Id == expected.Node.Id);
            Assert.AreEqual(expected.IncomingDegree, actual.IncomingDegree);
            Assert.AreEqual(expected.OutgoingDegree, actual.OutgoingDegree);
            Assert.AreEqual(expected.TotalCentrality, actual.TotalCentrality);
        }
    }

    [TestMethod]
    public void UpdateDegreeCentrality_ReusesOnlyUnaffectedExactBaseValues()
    {
        var graph = CreateGraph();
        var snapshot = new GraphProjectionSnapshot(Key, 1, graph.Projection);
        var previous = snapshot.DegreeCentralityVersioned(cancellationToken: TestContext.CancellationToken);
        var addedEdge = CreateEdge(20, graph.Third, graph.Fourth);
        var delta = new GraphProjectionDeltaBuilder(Key, 1, 2).AddEdges([addedEdge]).Build();
        var projectionUpdate = snapshot.ApplyDelta(delta, TestContext.CancellationToken);

        var update = projectionUpdate.UpdateDegreeCentrality(previous, cancellationToken: TestContext.CancellationToken);
        var full = projectionUpdate.Snapshot.Projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(GraphIncrementalAlgorithmUpdateMode.Incremental, update.Mode);
        Assert.AreEqual(2, update.ReusedNodeCount);
        Assert.AreEqual(2, update.RecomputedNodeCount);
        AssertDegreeResultsEqual(full, update.VersionedResult.Result);
        Assert.Contains("exact base version 1", update.Explanation);
    }

    [TestMethod]
    public void UpdateDegreeCentrality_WhenNodeCountChanges_ReusesRawDegreesAndRenormalizes()
    {
        var graph = CreateGraph();
        var snapshot = new GraphProjectionSnapshot(Key, 1, graph.Projection);
        var previous = snapshot.DegreeCentralityVersioned(cancellationToken: TestContext.CancellationToken);
        var added = CreateNode(5);
        var projectionUpdate = snapshot.ApplyDelta(
            new GraphProjectionDeltaBuilder(Key, 1, 2).AddNodes([added]).Build(),
            TestContext.CancellationToken);

        var update = projectionUpdate.UpdateDegreeCentrality(previous, cancellationToken: TestContext.CancellationToken);
        var full = projectionUpdate.Snapshot.Projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(4, update.ReusedNodeCount);
        Assert.AreEqual(1, update.RecomputedNodeCount);
        AssertDegreeResultsEqual(full, update.VersionedResult.Result);
    }

    [TestMethod]
    public void UpdateDegreeCentrality_WithSkippedBaseVersion_FallsBackToFullRecompute()
    {
        var graph = CreateGraph();
        var versionOne = new GraphProjectionSnapshot(Key, 1, graph.Projection);
        var previous = versionOne.DegreeCentralityVersioned(cancellationToken: TestContext.CancellationToken);
        var versionTwo = new GraphProjectionSnapshot(Key, 2, graph.Projection);
        var projectionUpdate = versionTwo.ApplyDelta(
            new GraphProjectionDeltaBuilder(Key, 2, 3).Build(),
            TestContext.CancellationToken);

        var update = projectionUpdate.UpdateDegreeCentrality(previous, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(GraphIncrementalAlgorithmUpdateMode.FullRecompute, update.Mode);
        Assert.AreEqual(0, update.ReusedNodeCount);
        Assert.AreEqual(4, update.RecomputedNodeCount);
    }

    [TestMethod]
    public void UpdateDegreeCentrality_WhenCancelled_DoesNotReturnPartialResult()
    {
        var graph = CreateGraph();
        var snapshot = new GraphProjectionSnapshot(Key, 1, graph.Projection);
        var previous = snapshot.DegreeCentralityVersioned(cancellationToken: TestContext.CancellationToken);
        var projectionUpdate = snapshot.ApplyDelta(
            new GraphProjectionDeltaBuilder(Key, 1, 2).Build(),
            TestContext.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => projectionUpdate.UpdateDegreeCentrality(previous, cancellationToken: cancellation.Token));
    }

    private static void AssertDegreeResultsEqual(GraphDegreeCentralityResult expected, GraphDegreeCentralityResult actual)
    {
        Assert.HasCount(expected.Scores.Count, actual.Scores);
        foreach (var expectedScore in expected.Scores)
        {
            var actualScore = actual.Scores.Single(score => score.Node.Id == expectedScore.Node.Id);
            Assert.AreEqual(expectedScore.IncomingDegree, actualScore.IncomingDegree);
            Assert.AreEqual(expectedScore.OutgoingDegree, actualScore.OutgoingDegree);
            Assert.AreEqual(expectedScore.IncomingCentrality, actualScore.IncomingCentrality);
            Assert.AreEqual(expectedScore.OutgoingCentrality, actualScore.OutgoingCentrality);
            Assert.AreEqual(expectedScore.TotalCentrality, actualScore.TotalCentrality);
        }
    }

    private static AlgorithmGraph CreateGraph()
    {
        var first = CreateNode(1);
        var second = CreateNode(2);
        var third = CreateNode(3);
        var fourth = CreateNode(4);
        var projection = GraphProjection.Create([first, second, third, fourth], [CreateEdge(11, first, second), CreateEdge(12, second, third)]);
        return new AlgorithmGraph(projection, third, fourth);
    }

    private static AlgorithmNode CreateNode(int identifier) => new()
    {
        Id = CreateGuid(identifier),
        Name = $"node-{identifier}"
    };

    private static AlgorithmEdge CreateEdge(int identifier, AlgorithmNode from, AlgorithmNode to) => new()
    {
        Id = CreateGuid(identifier),
        FromId = from.Id,
        ToId = to.Id
    };

    private static Guid CreateGuid(int identifier) =>
        Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}");

    private sealed record AlgorithmGraph(
        GraphProjection Projection,
        AlgorithmNode Third,
        AlgorithmNode Fourth);

    private sealed class AlgorithmNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class AlgorithmEdge : Edge;
}