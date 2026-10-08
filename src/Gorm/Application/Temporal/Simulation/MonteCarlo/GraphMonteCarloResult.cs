using System.Collections.ObjectModel;

namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

/// <summary>
/// Contains the full reproducible sample and its uncertainty statistics.
/// </summary>
public sealed class GraphMonteCarloResult
{
    private readonly double[] _sortedValues;

    internal GraphMonteCarloResult(
        GraphMonteCarloRunResult[] runs,
        ulong seed,
        double mean,
        double sampleStandardDeviation,
        double standardError,
        GraphConfidenceInterval confidenceInterval)
    {
        Runs = Array.AsReadOnly(runs);
        Seed = seed;
        Mean = mean;
        SampleStandardDeviation = sampleStandardDeviation;
        StandardError = standardError;
        ConfidenceInterval = confidenceInterval;
        Minimum = runs.Min(run => run.Value);
        Maximum = runs.Max(run => run.Value);
        _sortedValues = [.. runs.Select(run => run.Value).Order()];
        Assumptions = "Runs are independent conditional on the model; the reported mean interval uses a two-sided normal approximation.";
    }

    /// <summary>
    /// Gets every outcome in run order.
    /// </summary>
    public ReadOnlyCollection<GraphMonteCarloRunResult> Runs { get; }

    /// <summary>
    /// Gets the explicit experiment seed.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the arithmetic sample mean.
    /// </summary>
    public double Mean { get; }

    /// <summary>
    /// Gets the unbiased sample standard deviation, or zero for one run.
    /// </summary>
    public double SampleStandardDeviation { get; }

    /// <summary>
    /// Gets the estimated standard error of the sample mean.
    /// </summary>
    public double StandardError { get; }

    /// <summary>
    /// Gets the two-sided confidence interval of the sample mean.
    /// </summary>
    public GraphConfidenceInterval ConfidenceInterval { get; }

    /// <summary>
    /// Gets the smallest observed outcome.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the largest observed outcome.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets the statistical assumptions that consumers must consider.
    /// </summary>
    public string Assumptions { get; }

    /// <summary>
    /// Calculates a linearly interpolated sample percentile for a probability from zero through one.
    /// </summary>
    public double Percentile(double probability)
    {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(probability));
        }

        var position = probability * (_sortedValues.Length - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return _sortedValues[lowerIndex];
        }

        var fraction = position - lowerIndex;
        return _sortedValues[lowerIndex] + ((_sortedValues[upperIndex] - _sortedValues[lowerIndex]) * fraction);
    }
}