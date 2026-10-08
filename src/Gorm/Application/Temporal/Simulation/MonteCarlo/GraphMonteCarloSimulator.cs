using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Simulation.MonteCarlo;

/// <summary>
/// Runs bounded seed-reproducible experiments against an immutable temporal world.
/// </summary>
public static class GraphMonteCarloSimulator
{
    /// <summary>
    /// Executes a scalar simulation model once per deterministically derived run seed.
    /// </summary>
    public static GraphMonteCarloResult Run(
        GraphWorldSnapshot baseline,
        Func<GraphWorldSnapshot, GraphMonteCarloRunContext, double> simulation,
        GraphMonteCarloOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(simulation);
        options ??= new GraphMonteCarloOptions();
        ValidateOptions(options);

        var runs = new GraphMonteCarloRunResult[options.Runs];
        var sum = 0d;
        for (var runIndex = 0; runIndex < runs.Length; runIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runSeed = GraphDeterministicRandom.DeriveSeed(options.Seed, runIndex);
            var context = new GraphMonteCarloRunContext(runIndex, runSeed);
            var value = simulation(baseline, context);
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException(
                    $"Monte Carlo run {runIndex} returned non-finite outcome '{value}'.");
            }

            runs[runIndex] = new GraphMonteCarloRunResult
            {
                RunIndex = runIndex,
                Seed = runSeed,
                Value = value
            };
            sum += value;
        }

        var mean = sum / runs.Length;
        var squaredDeviations = runs.Sum(run => (run.Value - mean) * (run.Value - mean));
        var standardDeviation = runs.Length > 1 ? Math.Sqrt(squaredDeviations / (runs.Length - 1)) : 0;
        var standardError = standardDeviation / Math.Sqrt(runs.Length);
        var criticalValue = InverseStandardNormal((1 + options.ConfidenceLevel) / 2);
        var margin = criticalValue * standardError;
        return new GraphMonteCarloResult(
            runs,
            options.Seed,
            mean,
            standardDeviation,
            standardError,
            new GraphConfidenceInterval
            {
                Level = options.ConfidenceLevel,
                Lower = mean - margin,
                Upper = mean + margin,
                CriticalValue = criticalValue
            });
    }

    private static void ValidateOptions(GraphMonteCarloOptions options)
    {
        if (options.Runs <= 0 || options.MaximumRuns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Run limits must be positive.");
        }

        if (options.Runs > options.MaximumRuns)
        {
            throw new InvalidOperationException($"Requested runs exceed MaximumRuns ({options.MaximumRuns}).");
        }

        if (!double.IsFinite(options.ConfidenceLevel) ||
            options.ConfidenceLevel <= 0 ||
            options.ConfidenceLevel >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ConfidenceLevel must be between zero and one.");
        }
    }

    private static double InverseStandardNormal(double probability)
    {
        // Peter J. Acklam's rational approximation of the inverse standard normal
        // cumulative distribution function. Separate approximations are used for
        // the lower tail, central region, and upper tail for numerical accuracy.
        const double LowerBoundary = 0.02425;
        const double UpperBoundary = 1 - LowerBoundary;

        // Polynomial coefficients for the central region.
        ReadOnlySpan<double> a = [-39.69683028665376, 220.9460984245205, -275.9285104469687, 138.3577518672690, -30.66479806614716, 2.506628277459239];
        ReadOnlySpan<double> b = [-54.47609879822406, 161.5858368580409, -155.6989798598866, 66.80131188771972, -13.28068155288572];

        // Polynomial coefficients for the lower and upper tails.
        ReadOnlySpan<double> c = [-0.007784894002430293, -0.3223964580411365, -2.400758277161838, -2.549732539343734, 4.374664141464968, 2.938163982698783];
        ReadOnlySpan<double> d = [0.007784695709041462, 0.3224671290700398, 2.445134137142996, 3.754408661907416];

        // Lower tail: probabilities close to zero require a logarithmic transformation.
        if (probability < LowerBoundary)
        {
            var q = Math.Sqrt(-2 * Math.Log(probability));
            return ((((((((((c[0] * q) + c[1]) * q) + c[2]) * q) + c[3]) * q) + c[4]) * q) + c[5]) /
                ((((((((d[0] * q) + d[1]) * q) + d[2]) * q) + d[3]) * q) + 1);
        }

        // Central region: approximate directly around the distribution's midpoint.
        if (probability <= UpperBoundary)
        {
            var q = probability - 0.5;
            var r = q * q;
            return ((((((((((a[0] * r) + a[1]) * r) + a[2]) * r) + a[3]) * r) + a[4]) * r) + a[5]) * q /
                ((((((((((b[0] * r) + b[1]) * r) + b[2]) * r) + b[3]) * r) + b[4]) * r) + 1);
        }

        // Upper tail: mirror the lower-tail approximation around zero.
        var upperQ = Math.Sqrt(-2 * Math.Log(1 - probability));
        return -((((((((((c[0] * upperQ) + c[1]) * upperQ) + c[2]) * upperQ) + c[3]) * upperQ) + c[4]) * upperQ) + c[5]) /
            ((((((((d[0] * upperQ) + d[1]) * upperQ) + d[2]) * upperQ) + d[3]) * upperQ) + 1);
    }
}