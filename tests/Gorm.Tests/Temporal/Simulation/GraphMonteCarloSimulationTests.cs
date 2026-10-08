using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Simulation.MonteCarlo;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Tests.Temporal.Simulation;

[TestClass]
public sealed class GraphMonteCarloSimulationTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Run_ReproducesEveryOutcomeFromTheSameSeed()
    {
        var options = new GraphMonteCarloOptions { Runs = 20, Seed = 42 };
        var first = GraphMonteCarloSimulator.Run(Baseline(), (_, run) => run.NextDouble(), options, TestContext.CancellationToken);
        var second = GraphMonteCarloSimulator.Run(Baseline(), (_, run) => run.NextDouble(), options, TestContext.CancellationToken);

        CollectionAssert.AreEqual(first.Runs.Select(run => run.Value).ToArray(), second.Runs.Select(run => run.Value).ToArray());
        CollectionAssert.AreEqual(first.Runs.Select(run => run.Seed).ToArray(), second.Runs.Select(run => run.Seed).ToArray());
    }

    [TestMethod]
    public void Run_ChangesTheSampleWhenTheSeedChanges()
    {
        var first = GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, run) => run.NextDouble(),
            new GraphMonteCarloOptions { Runs = 5, Seed = 1 },
            TestContext.CancellationToken);
        var second = GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, run) => run.NextDouble(),
            new GraphMonteCarloOptions { Runs = 5, Seed = 2 },
            TestContext.CancellationToken);

        Assert.AreNotEqual(first.Runs[0].Value, second.Runs[0].Value);
    }

    [TestMethod]
    public void Run_CalculatesDistributionAndConfidenceEvidence()
    {
        var result = GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, run) => run.RunIndex,
            new GraphMonteCarloOptions { Runs = 5, Seed = 7, ConfidenceLevel = 0.95 },
            TestContext.CancellationToken);

        Assert.AreEqual(2d, result.Mean);
        Assert.AreEqual(0d, result.Minimum);
        Assert.AreEqual(4d, result.Maximum);
        Assert.AreEqual(1d, result.Percentile(0.25));
        Assert.AreEqual(3d, result.Percentile(0.75));
        Assert.IsGreaterThan(0d, result.SampleStandardDeviation);
        Assert.IsLessThan(result.Mean, result.ConfidenceInterval.Lower);
        Assert.IsGreaterThan(result.Mean, result.ConfidenceInterval.Upper);
        Assert.IsTrue(result.Assumptions.Contains("normal approximation", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Run_ConstantOutcomeHasZeroUncertainty()
    {
        var result = GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, _) => 42,
            new GraphMonteCarloOptions { Runs = 10, Seed = 99 },
            TestContext.CancellationToken);

        Assert.AreEqual(42d, result.Mean);
        Assert.AreEqual(0d, result.SampleStandardDeviation);
        Assert.AreEqual(0d, result.StandardError);
        Assert.AreEqual(42d, result.ConfidenceInterval.Lower);
        Assert.AreEqual(42d, result.ConfidenceInterval.Upper);
    }

    [TestMethod]
    public void Run_ProvidesDeterministicIntegerAndBernoulliInputs()
    {
        var result = GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, run) => run.NextInt32(10) + (run.NextBoolean(1) ? 10 : 0),
            new GraphMonteCarloOptions { Runs = 10, Seed = 123 },
            TestContext.CancellationToken);

        Assert.IsTrue(result.Runs.All(run => run.Value >= 10 && run.Value < 20));
    }

    [TestMethod]
    public void Run_RejectsInvalidOptionsAndNonFiniteOutcomes()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, _) => 1,
            new GraphMonteCarloOptions { Runs = 2, MaximumRuns = 1 },
            TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, _) => 1,
            new GraphMonteCarloOptions { Runs = 1, ConfidenceLevel = 1 },
            TestContext.CancellationToken));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, _) => double.NaN,
            new GraphMonteCarloOptions { Runs = 1 },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void Run_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => GraphMonteCarloSimulator.Run(
            Baseline(),
            (_, _) => 1,
            new GraphMonteCarloOptions { Runs = 10 },
            cancellation.Token));
    }

    private static GraphWorldSnapshot Baseline() =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("monte-carlo/eu"),
                Version = 1,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [],
                Edges = []
            });
}