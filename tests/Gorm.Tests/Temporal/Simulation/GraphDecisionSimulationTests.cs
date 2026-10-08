using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Simulation.Decision;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Tests.Temporal.Simulation;

[TestClass]
public sealed class GraphDecisionSimulationTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] Expected = ["alpha", "zeta"];

    [TestMethod]
    public void Sensitivity_CalculatesDirectionElasticityAndImpact()
    {
        var result = GraphSensitivityAnalyzer.Analyze(
            Baseline(),
            new GraphSensitivityDefinition
            {
                ParameterName = "capacity",
                BaselineValue = 10,
                Values = [15d, 5d],
                OutcomeSelector = (_, value) => value * 2
            },
            TestContext.CancellationToken);

        Assert.AreEqual(20d, result.BaselineOutcome);
        Assert.AreEqual(GraphSensitivityDirection.Increasing, result.Direction);
        Assert.AreEqual(20d, result.OutcomeRange);
        Assert.AreEqual(1d, result.Points.Single(point => point.ParameterValue == 15).Elasticity);
        Assert.AreEqual(5d, result.MostImpactfulPoint.ParameterValue);
    }

    [TestMethod]
    public void Sensitivity_ClassifiesMixedAndRejectsInvalidSamples()
    {
        var mixed = GraphSensitivityAnalyzer.Analyze(
            Baseline(),
            new GraphSensitivityDefinition
            {
                ParameterName = "risk",
                BaselineValue = 0,
                Values = [-1d, 1d],
                OutcomeSelector = (_, value) => value * value
            },
            TestContext.CancellationToken);
        Assert.AreEqual(GraphSensitivityDirection.Mixed, mixed.Direction);

        Assert.ThrowsExactly<ArgumentException>(() => GraphSensitivityAnalyzer.Analyze(
            Baseline(),
            new GraphSensitivityDefinition
            {
                ParameterName = "duplicate",
                BaselineValue = 1,
                Values = [2d, 2d],
                OutcomeSelector = (_, value) => value
            },
            TestContext.CancellationToken));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphSensitivityAnalyzer.Analyze(
            Baseline(),
            new GraphSensitivityDefinition
            {
                ParameterName = "nan",
                BaselineValue = 1,
                Values = [2d],
                OutcomeSelector = (_, _) => double.NaN
            },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void PolicyComparison_RanksMinimizeAndMaximizeGoalsWithEvidence()
    {
        var result = GraphPolicyComparator.Compare(
            Baseline(),
            [
                Candidate("resilient", 100, 0.99),
                Candidate("cheap", 50, 0.80)
            ],
            [
                new GraphPolicyCriterion { Name = "cost", Goal = GraphPolicyGoal.Minimize, Weight = 1 },
                new GraphPolicyCriterion { Name = "reliability", Goal = GraphPolicyGoal.Maximize, Weight = 2 }
            ],
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("resilient", result.Best.CandidateId);
        Assert.AreEqual(1, result.Best.Rank);
        Assert.IsGreaterThan(result.Ranking[1].Score, result.Best.Score);
        Assert.HasCount(2, result.Best.Criteria);
        Assert.IsTrue(result.Assumptions.Contains("weights", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PolicyComparison_BreaksExactTiesByStableId()
    {
        var result = GraphPolicyComparator.Compare(
            Baseline(),
            [Candidate("zeta", 10, 1), Candidate("alpha", 10, 1)],
            [new GraphPolicyCriterion { Name = "cost", Goal = GraphPolicyGoal.Minimize }],
            cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            Expected,
            result.Ranking.Select(score => score.CandidateId).ToArray());
        Assert.AreEqual(1d, result.Ranking[0].Score);
    }

    [TestMethod]
    public void PolicyComparison_RejectsMissingMetricsAndLimits()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphPolicyComparator.Compare(
            Baseline(),
            [
                new GraphPolicyCandidate
                {
                    Id = "missing",
                    Evaluate = _ => new Dictionary<string, double>()
                }
            ],
            [new GraphPolicyCriterion { Name = "cost", Goal = GraphPolicyGoal.Minimize }],
            cancellationToken: TestContext.CancellationToken));

        Assert.ThrowsExactly<InvalidOperationException>(() => GraphPolicyComparator.Compare(
            Baseline(),
            [Candidate("one", 1, 1), Candidate("two", 2, 1)],
            [new GraphPolicyCriterion { Name = "cost", Goal = GraphPolicyGoal.Minimize }],
            new GraphPolicyComparisonOptions { MaximumCandidates = 1 },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void DecisionAnalyses_ObserveCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => GraphSensitivityAnalyzer.Analyze(
            Baseline(),
            new GraphSensitivityDefinition
            {
                ParameterName = "cancel",
                BaselineValue = 1,
                Values = [2d],
                OutcomeSelector = (_, value) => value
            },
            cancellation.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphPolicyComparator.Compare(
            Baseline(),
            [Candidate("one", 1, 1)],
            [new GraphPolicyCriterion { Name = "cost", Goal = GraphPolicyGoal.Minimize }],
            cancellationToken: cancellation.Token));
    }

    private static GraphPolicyCandidate Candidate(string id, double cost, double reliability) =>
        new()
        {
            Id = id,
            Evaluate = _ => new Dictionary<string, double>
            {
                ["cost"] = cost,
                ["reliability"] = reliability
            }
        };

    private static GraphWorldSnapshot Baseline() =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("decision/eu"),
                Version = 1,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [],
                Edges = []
            });
}