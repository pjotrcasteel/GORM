using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Algorithms.LinkPrediction;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Algorithms;

[TestClass]
public sealed class GraphCommunityIntelligenceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void DetectCommunities_SeparatesWeightedClustersAndDescribesBoundary()
    {
        var graph = CreateTwoClusterGraph();

        var result = graph.Projection.DetectCommunities(new GraphCommunityDetectionOptions
        {
            WeightSelector = edge => ((CommunityEdge)edge).Weight,
            AnomalyThreshold = 0.01,
            RandomSeed = 123
        },
        TestContext.CancellationToken);

        Assert.HasCount(2, result.Communities);
        Assert.AreEqual(result.GetCommunityId(graph.Left[0].Id), result.GetCommunityId(graph.Left[2].Id));
        Assert.AreEqual(result.GetCommunityId(graph.Right[0].Id), result.GetCommunityId(graph.Right[2].Id));
        Assert.AreNotEqual(result.GetCommunityId(graph.Left[0].Id), result.GetCommunityId(graph.Right[0].Id));
        Assert.IsGreaterThan(0d, result.Modularity);
        Assert.HasCount(1, result.BoundaryEdges);
        Assert.HasCount(2, result.BridgeNodes);
        Assert.HasCount(1, result.Anomalies);
        Assert.IsGreaterThan(0, result.BoundaryEdges[0].Explanation.Length);
        Assert.IsTrue(result.Communities.All(community => community.Conductance >= 0));
    }

    [TestMethod]
    public void DetectCommunities_WithSameSeed_IsDeterministic()
    {
        var graph = CreateTwoClusterGraph();
        var options = new GraphCommunityDetectionOptions
        {
            WeightSelector = edge => ((CommunityEdge)edge).Weight,
            RandomSeed = 9876
        };

        var first = graph.Projection.DetectCommunities(options, TestContext.CancellationToken);
        var second = graph.Projection.DetectCommunities(options, TestContext.CancellationToken);

        Assert.IsGreaterThan(0, first.Levels.Count);
        CollectionAssert.AreEqual(
            graph.Projection.Nodes.Select(node => first.GetCommunityId(node.Id)).ToArray(),
            graph.Projection.Nodes.Select(node => second.GetCommunityId(node.Id)).ToArray());
        Assert.AreEqual(first.Modularity, second.Modularity);
    }

    [TestMethod]
    public void DetectCommunities_InDirectedMode_PreservesDirectedModularity()
    {
        var graph = CreateTwoClusterGraph();

        var result = graph.Projection.DetectCommunities(new GraphCommunityDetectionOptions
        {
            EdgeMode = GraphCommunityEdgeMode.Directed,
            WeightSelector = edge => ((CommunityEdge)edge).Weight,
            RandomSeed = 42
        }, TestContext.CancellationToken);

        Assert.HasCount(2, result.Communities);
        Assert.IsGreaterThan(0d, result.Modularity);
    }

    [TestMethod]
    public void DetectCommunities_WithInvalidWeight_ThrowsInvalidOperationException()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var projection = GraphProjection.Create([first, second], [CreateEdge(first, second)]);

        Assert.ThrowsExactly<InvalidOperationException>(() => projection.DetectCommunities(
            new GraphCommunityDetectionOptions { WeightSelector = _ => -1 }, TestContext.CancellationToken));
    }

    [TestMethod]
    public void PredictLinks_RanksPairWithThreeSharedNeighboursHighestAndExplainsEvidence()
    {
        var source = CreateNode("source");
        var target = CreateNode("target");
        var firstEvidence = CreateNode("first-evidence");
        var secondEvidence = CreateNode("second-evidence");
        var thirdEvidence = CreateNode("third-evidence");
        var projection = GraphProjection.Create(
            [source, target, firstEvidence, secondEvidence, thirdEvidence],
            [
                CreateEdge(source, firstEvidence),
                CreateEdge(source, secondEvidence),
                CreateEdge(source, thirdEvidence),
                CreateEdge(target, firstEvidence),
                CreateEdge(target, secondEvidence),
                CreateEdge(target, thirdEvidence)
            ]);

        var result = projection.PredictLinks(cancellationToken: TestContext.CancellationToken);
        var prediction = result.Predictions[0];

        CollectionAssert.AreEquivalent(new[] { source.Id, target.Id }, new[] { prediction.Source.Id, prediction.Target.Id });
        Assert.AreEqual(3, prediction.Metrics.CommonNeighbors);
        Assert.HasCount(3, prediction.EvidenceNodes);
        Assert.IsGreaterThan(0d, prediction.Confidence);
        Assert.Contains("3 shared evidence node(s)", prediction.Explanation);
    }

    [TestMethod]
    public void PredictLinks_InDirectedMode_UsesTwoHopPathAsEvidence()
    {
        var source = CreateNode("source");
        var intermediary = CreateNode("intermediary");
        var target = CreateNode("target");
        var projection = GraphProjection.Create([source, intermediary, target], [CreateEdge(source, intermediary), CreateEdge(intermediary, target)]);

        var result = projection.PredictLinks(new GraphLinkPredictionOptions { Directed = true }, cancellationToken: TestContext.CancellationToken);
        var prediction = result.Predictions.Single(item => item.Source.Id == source.Id && item.Target.Id == target.Id);

        Assert.AreEqual(1, prediction.Metrics.CommonNeighbors);
        Assert.AreEqual(intermediary.Id, prediction.EvidenceNodes[0].Id);
    }

    [TestMethod]
    public void PredictLinks_WithCommunityScope_OnlyReturnsPairsInSameCommunity()
    {
        var source = CreateNode("source");
        var target = CreateNode("target");
        var evidence = CreateNode("evidence");
        var projection = GraphProjection.Create([source, target, evidence], [CreateEdge(source, evidence), CreateEdge(target, evidence)]);
        var communities = projection.DetectCommunities(cancellationToken: TestContext.CancellationToken);

        var result = projection.PredictLinks(
            new GraphLinkPredictionOptions
            {
                CandidateScope = GraphLinkPredictionCandidateScope.WithinCommunity
            },
            communities,
            TestContext.CancellationToken);

        Assert.IsGreaterThan(0, result.Predictions.Count);
        Assert.IsTrue(result.Predictions.All(prediction => communities.GetCommunityId(prediction.Source.Id) == communities.GetCommunityId(prediction.Target.Id)));
    }

    [TestMethod]
    public void PredictLinks_WhenCandidateLimitIsExceeded_ThrowsSpecificException()
    {
        var projection = GraphProjection.Create([CreateNode("first"), CreateNode("second"), CreateNode("third")], []);

        var exception = Assert.ThrowsExactly<GraphLinkPredictionCandidateLimitException>(() =>
            projection.PredictLinks(new GraphLinkPredictionOptions
            {
                MaximumCandidatePairs = 1,
                MinimumCommonNeighbors = 0
            }, cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual(1, exception.Limit);
    }

    private static TwoClusterGraph CreateTwoClusterGraph()
    {
        var left = new[] { CreateNode("left-1"), CreateNode("left-2"), CreateNode("left-3") };
        var right = new[] { CreateNode("right-1"), CreateNode("right-2"), CreateNode("right-3") };
        var projection = GraphProjection.Create(
            left.Concat(right),
            [
                CreateEdge(left[0], left[1]),
                CreateEdge(left[1], left[2]),
                CreateEdge(left[2], left[0]),
                CreateEdge(right[0], right[1]),
                CreateEdge(right[1], right[2]),
                CreateEdge(right[2], right[0]),
                CreateEdge(left[2], right[0], weight: 0.05)
            ]);
        return new TwoClusterGraph(projection, left, right);
    }

    private static CommunityNode CreateNode(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name
    };

    private static CommunityEdge CreateEdge(CommunityNode from, CommunityNode to, double weight = 1) => new()
    {
        Id = Guid.NewGuid(),
        FromId = from.Id,
        ToId = to.Id,
        Weight = weight
    };

    private sealed record TwoClusterGraph(GraphProjection Projection, CommunityNode[] Left, CommunityNode[] Right);

    private sealed class CommunityNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CommunityEdge : Edge
    {
        public double Weight { get; set; }
    }
}