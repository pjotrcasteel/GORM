using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Flow;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Algorithms.Planning;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Algorithms;

[TestClass]
public sealed class GraphOptimizationIntelligenceTests
{
    private static readonly double[] ExpectedPathCosts = [2d, 4d, 5d, 6d];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void AStarPath_ReturnsMinimumCostRouteAndExplorationEvidence()
    {
        var graph = CreateRouteGraph();

        var result = graph.Projection.AStarPath(
            graph.Source.Id,
            graph.Destination.Id,
            new GraphAStarOptions
            {
                CostSelector = edge => ((OptimizationEdge)edge).Cost,
                Heuristic = (_, _) => 0
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Path.TotalCost);
        CollectionAssert.AreEqual(
            new[] { graph.Source.Id, graph.Fast.Id, graph.Destination.Id },
            result.Path.Nodes.Select(node => node.Id).ToArray());
        Assert.IsGreaterThan(0, result.ExploredNodeCount);
        Assert.Contains("minimum-cost route", result.Path.Explanation);
    }

    [TestMethod]
    public void AStarPath_WithInvalidHeuristic_ThrowsInvalidOperationException()
    {
        var graph = CreateRouteGraph();

        Assert.ThrowsExactly<InvalidOperationException>(() => graph.Projection.AStarPath(
            graph.Source.Id,
            graph.Destination.Id,
            new GraphAStarOptions { Heuristic = (_, _) => -1 },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void KShortestPaths_ReturnsUniqueLooplessPathsInCostOrder()
    {
        var graph = CreateRouteGraph();

        var result = graph.Projection.KShortestPaths(
            graph.Source.Id,
            graph.Destination.Id,
            new GraphKShortestPathsOptions
            {
                CostSelector = edge => ((OptimizationEdge)edge).Cost,
                MaximumPaths = 5
            },
            TestContext.CancellationToken);

        Assert.HasCount(4, result.Paths);
        CollectionAssert.AreEqual(
            ExpectedPathCosts,
            result.Paths.Select(path => path.TotalCost).ToArray());
        Assert.IsTrue(result.Paths.All(path => path.Nodes.Select(node => node.Id).Distinct().Count() == path.Nodes.Count));
        Assert.IsTrue(result.Exhausted);
        Assert.IsFalse(result.Truncated);
    }

    [TestMethod]
    public void KShortestPaths_WhenSpurLimitIsReached_MarksResultTruncated()
    {
        var graph = CreateRouteGraph();

        var result = graph.Projection.KShortestPaths(
            graph.Source.Id,
            graph.Destination.Id,
            new GraphKShortestPathsOptions
            {
                CostSelector = edge => ((OptimizationEdge)edge).Cost,
                MaximumPaths = 5,
                MaximumSpurSearches = 1
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Truncated);
        Assert.IsGreaterThan(0, result.Paths.Count);
    }

    [TestMethod]
    public void MaximumFlow_ReturnsMatchingFlowCutAndBottlenecks()
    {
        var graph = CreateFlowGraph();

        var result = graph.Projection.MaximumFlow(
            graph.Source.Id,
            graph.Destination.Id,
            new GraphMaximumFlowOptions
            {
                CapacitySelector = edge => ((OptimizationEdge)edge).Capacity
            },
            TestContext.CancellationToken);

        Assert.AreEqual(5, result.MaximumFlow);
        Assert.AreEqual(5, result.MinimumCutCapacity);
        Assert.HasCount(2, result.CutEdges);
        Assert.IsGreaterThan(0, result.BottleneckEdges.Count);
        Assert.IsTrue(result.CutEdges.All(edge => edge.IsSaturated));
        Assert.Contains("equals minimum-cut capacity", result.Explanation);
    }

    [TestMethod]
    public void MaximumFlow_WhenAugmentationLimitIsExceeded_ThrowsSpecificException()
    {
        var graph = CreateFlowGraph();

        var exception = Assert.ThrowsExactly<GraphFlowAugmentationLimitException>(() =>
            graph.Projection.MaximumFlow(
                graph.Source.Id,
                graph.Destination.Id,
                new GraphMaximumFlowOptions
                {
                    CapacitySelector = edge => ((OptimizationEdge)edge).Capacity,
                    MaximumAugmentations = 1
                },
                TestContext.CancellationToken));

        Assert.AreEqual(1, exception.Limit);
    }

    [TestMethod]
    public void ParetoRoutes_ReturnsTradeoffFrontierAndExplainsDominatedAlternative()
    {
        var graph = CreateRouteGraph();
        var options = CreateParetoOptions();

        var result = graph.Projection.ParetoRoutes(
            graph.Source.Id,
            graph.Destination.Id,
            options,
            TestContext.CancellationToken);

        Assert.HasCount(3, result.Routes);
        Assert.IsFalse(result.Truncated);
        Assert.IsTrue(result.Routes.All(route => route.Explanation.Length > 0));
        Assert.IsTrue(result.Routes.Any(route => route.Metrics["cost"] == 2 && route.Metrics["risk"] == 10));
        Assert.IsTrue(result.Routes.Any(route => route.Metrics["cost"] == 6 && route.Metrics["risk"] == 2));
        Assert.IsGreaterThan(0, result.RejectedAlternatives.Count);
        Assert.Contains("Dominated", result.RejectedAlternatives[0].Reason);
    }

    [TestMethod]
    public void ParetoRoutes_WhenExpansionLimitIsReached_MarksResultTruncated()
    {
        var graph = CreateRouteGraph();
        var options = CreateParetoOptions();
        options.MaximumExpandedLabels = 1;

        var result = graph.Projection.ParetoRoutes(
            graph.Source.Id,
            graph.Destination.Id,
            options,
            TestContext.CancellationToken);

        Assert.IsTrue(result.Truncated);
    }

    [TestMethod]
    public void RerouteAroundImpact_SelectsBestAvailableRouteAndReportsDelta()
    {
        var graph = CreateRouteGraph();
        var options = new GraphImpactReroutingOptions
        {
            CostSelector = edge => ((OptimizationEdge)edge).Cost,
            MaximumAlternativePaths = 3
        };
        options.FailedEdgeIds.Add(graph.FastLast.Id);

        var result = graph.Projection.RerouteAroundImpact(
            graph.Source.Id,
            graph.Destination.Id,
            options,
            TestContext.CancellationToken);

        Assert.IsTrue(result.RouteAvailable);
        Assert.IsNotNull(result.BaselinePath);
        Assert.IsNotNull(result.RecommendedPath);
        Assert.AreEqual(2, result.BaselinePath.TotalCost);
        Assert.AreEqual(4, result.RecommendedPath.TotalCost);
        Assert.AreEqual(2d, result.AdditionalCost);
        Assert.IsTrue(result.RecommendedPath.Edges.All(edge => edge.Id != graph.FastLast.Id));
        Assert.Contains("failed edge(s)", result.Explanation);
    }

    [TestMethod]
    public void RerouteAroundImpact_WhenDestinationFails_ReturnsUnavailableExplanation()
    {
        var graph = CreateRouteGraph();
        var options = new GraphImpactReroutingOptions();
        options.FailedNodeIds.Add(graph.Destination.Id);

        var result = graph.Projection.RerouteAroundImpact(
            graph.Source.Id,
            graph.Destination.Id,
            options,
            TestContext.CancellationToken);

        Assert.IsFalse(result.RouteAvailable);
        Assert.IsNull(result.RecommendedPath);
        Assert.Contains("source or destination node", result.Explanation);
    }

    private static GraphParetoRouteOptions CreateParetoOptions()
    {
        var options = new GraphParetoRouteOptions();
        options.Criteria.Add(new GraphRouteCriterion
        {
            Name = "cost",
            Selector = edge => ((OptimizationEdge)edge).Cost
        });
        options.Criteria.Add(new GraphRouteCriterion
        {
            Name = "risk",
            Selector = edge => ((OptimizationEdge)edge).Risk
        });
        return options;
    }

    private static RouteGraph CreateRouteGraph()
    {
        var source = CreateNode("source");
        var fast = CreateNode("fast");
        var balanced = CreateNode("balanced");
        var safe = CreateNode("safe");
        var dominated = CreateNode("dominated");
        var destination = CreateNode("destination");
        var fastFirst = CreateEdge(source, fast, cost: 1, risk: 5);
        var fastLast = CreateEdge(fast, destination, cost: 1, risk: 5);
        var projection = GraphProjection.Create(
            [source, fast, balanced, safe, dominated, destination],
            [
                fastFirst,
                fastLast,
                CreateEdge(source, balanced, cost: 2, risk: 2),
                CreateEdge(balanced, destination, cost: 2, risk: 2),
                CreateEdge(source, safe, cost: 3, risk: 1),
                CreateEdge(safe, destination, cost: 3, risk: 1),
                CreateEdge(source, dominated, cost: 2.5, risk: 2.5),
                CreateEdge(dominated, destination, cost: 2.5, risk: 2.5)
            ]);
        return new RouteGraph(projection, source, fast, destination, fastLast);
    }

    private static FlowGraph CreateFlowGraph()
    {
        var source = CreateNode("source");
        var left = CreateNode("left");
        var right = CreateNode("right");
        var destination = CreateNode("destination");
        var projection = GraphProjection.Create(
            [source, left, right, destination],
            [
                CreateEdge(source, left, capacity: 3),
                CreateEdge(source, right, capacity: 2),
                CreateEdge(left, right, capacity: 1),
                CreateEdge(left, destination, capacity: 2),
                CreateEdge(right, destination, capacity: 3)
            ]);
        return new FlowGraph(projection, source, destination);
    }

    private static OptimizationNode CreateNode(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name
    };

    private static OptimizationEdge CreateEdge(OptimizationNode from, OptimizationNode to, double cost = 1, double risk = 0, double capacity = 1) => new()
    {
        Id = Guid.NewGuid(),
        FromId = from.Id,
        ToId = to.Id,
        Cost = cost,
        Risk = risk,
        Capacity = capacity
    };

    private sealed record RouteGraph(
        GraphProjection Projection,
        OptimizationNode Source,
        OptimizationNode Fast,
        OptimizationNode Destination,
        OptimizationEdge FastLast);

    private sealed record FlowGraph(
        GraphProjection Projection,
        OptimizationNode Source,
        OptimizationNode Destination);

    private sealed class OptimizationNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OptimizationEdge : Edge
    {
        public double Cost { get; set; }

        public double Risk { get; set; }

        public double Capacity { get; set; }
    }
}