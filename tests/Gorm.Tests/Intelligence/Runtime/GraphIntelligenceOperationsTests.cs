using System.Diagnostics;
using Gorm.Application.Diagnostics;
using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Benchmarking;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Runtime;

[TestClass]
public sealed class GraphIntelligenceOperationsTests
{
    private static readonly string[] ExpectedBenchmarkOperations =
    [
        "projection.build",
        "projection.cache-hit",
        "degree-centrality.full",
        "degree-centrality.incremental-noop",
        "pagerank"
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Run_EmitsAlgorithmActivityThroughExistingGormSource()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GormDiagnostics.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        var projection = CreateProjection(4);

        projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);

        var activity = activities.Single(item => item.OperationName == GormIntelligenceDiagnostics.ActivityNames.Algorithm);
        Assert.AreEqual(ActivityStatusCode.Ok, activity.Status);
        Assert.AreEqual(4, activity.GetTagItem(GormIntelligenceDiagnostics.TagNames.NodeCount));
        Assert.AreEqual(3, activity.GetTagItem(GormIntelligenceDiagnostics.TagNames.EdgeCount));
        Assert.Contains(nameof(GraphDegreeCentralityAlgorithm), activity.GetTagItem(GormIntelligenceDiagnostics.TagNames.Algorithm)?.ToString() ?? string.Empty);
    }

    [TestMethod]
    public void PageRank_ReportsProgressAtStableIterationBoundaries()
    {
        var reports = new List<GraphAlgorithmProgress>();
        var projection = CreateProjection(4);

        projection.PageRank(new GraphPageRankOptions
        {
            MaximumIterations = 3,
            Tolerance = double.Epsilon,
            ProgressInterval = 2,
            Progress = new InlineProgress<GraphAlgorithmProgress>(reports.Add)
        }, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new long[] { 2, 3 }, reports.Select(report => report.Completed).ToArray());
        Assert.IsTrue(reports.All(report => report.Operation == "pagerank"));
    }

    [TestMethod]
    public void DegreeCentrality_ReportsCompletedNodeCount()
    {
        var reports = new List<GraphAlgorithmProgress>();
        var projection = CreateProjection(5);

        projection.DegreeCentrality(new GraphDegreeCentralityOptions
        {
            Progress = new InlineProgress<GraphAlgorithmProgress>(reports.Add)
        }, TestContext.CancellationToken);

        Assert.HasCount(1, reports);
        Assert.AreEqual(5, reports[0].Completed);
        Assert.AreEqual(1, reports[0].Fraction);
    }

    [TestMethod]
    public void StandardBenchmarkProfiles_HaveIncreasingDeterministicSizes()
    {
        Assert.IsGreaterThan(GraphIntelligenceBenchmarkProfile.Small.NodeCount, GraphIntelligenceBenchmarkProfile.Medium.NodeCount);
        Assert.IsGreaterThan(GraphIntelligenceBenchmarkProfile.Medium.NodeCount, GraphIntelligenceBenchmarkProfile.Large.NodeCount);
        Assert.AreEqual(5, GraphIntelligenceBenchmarkProfile.Small.EdgeCount / GraphIntelligenceBenchmarkProfile.Small.NodeCount);
    }

    [TestMethod]
    public async Task BenchmarkRunner_WithTinyProfile_ReturnsStableOperationSetAndAllocationData()
    {
        var progress = new List<GraphAlgorithmProgress>();
        var result = await GraphIntelligenceBenchmarkRunner.RunAsync(
            new GraphIntelligenceBenchmarkProfile("test", 20, 60),
            new GraphIntelligenceBenchmarkOptions
            {
                WarmupIterations = 0,
                MeasuredIterations = 1,
                PageRankIterations = 2,
                Progress = new InlineProgress<GraphAlgorithmProgress>(progress.Add)
            },
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            ExpectedBenchmarkOperations,
            result.Measurements.Select(measurement => measurement.Operation).ToArray());
        Assert.IsTrue(result.Measurements.All(measurement => measurement.MedianMilliseconds >= 0));
        Assert.IsTrue(result.Measurements.All(measurement => measurement.AverageAllocatedBytes >= 0));
        Assert.HasCount(5, progress);
    }

    [TestMethod]
    public async Task BenchmarkRunner_WhenCancelled_DoesNotStartGraphGeneration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            GraphIntelligenceBenchmarkRunner.RunAsync(
                new GraphIntelligenceBenchmarkProfile("cancelled", 10, 20),
                cancellationToken: cancellation.Token));
    }

    [TestMethod]
    public void PooledAlgorithms_RepeatedExecutionPreservesExactResults()
    {
        var projection = CreateProjection(20);
        var firstPageRank = projection.PageRank(cancellationToken: TestContext.CancellationToken);
        var firstDegree = projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);

        for (var iteration = 0; iteration < 10; iteration++)
        {
            var pageRank = projection.PageRank(cancellationToken: TestContext.CancellationToken);
            var degree = projection.DegreeCentrality(cancellationToken: TestContext.CancellationToken);
            foreach (var node in projection.Nodes)
            {
                Assert.AreEqual(firstPageRank.GetScore(node.Id), pageRank.GetScore(node.Id));
            }

            Assert.IsTrue(firstDegree.Scores.Select(score => score.Node.Id).SequenceEqual(degree.Scores.Select(score => score.Node.Id)));
        }
    }

    private static GraphProjection CreateProjection(int nodeCount)
    {
        var nodes = Enumerable.Range(1, nodeCount)
            .Select(index => new OperationsNode { Id = CreateGuid(index) })
            .ToArray();
        var edges = Enumerable.Range(1, Math.Max(nodeCount - 1, 0))
            .Select(index => new OperationsEdge
            {
                Id = CreateGuid(10_000 + index),
                FromId = nodes[index - 1].Id,
                ToId = nodes[index].Id
            })
            .ToArray();
        return GraphProjection.Create(nodes, edges);
    }

    private static Guid CreateGuid(int identifier) =>
        Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}");

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class OperationsNode : Node;

    private sealed class OperationsEdge : Edge;
}