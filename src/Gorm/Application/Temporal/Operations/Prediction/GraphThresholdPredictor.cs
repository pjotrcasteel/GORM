namespace Gorm.Application.Temporal.Operations.Prediction;

/// <summary>
/// Fits a bounded least-squares trend and predicts a directional threshold crossing.
/// </summary>
public static class GraphThresholdPredictor
{
    /// <summary>
    /// Predicts a crossing from finite unique time samples without changing twin state.
    /// </summary>
    public static GraphThresholdPrediction Predict(
        IReadOnlyCollection<GraphTwinMetricSample> samples,
        double threshold,
        GraphThresholdDirection direction,
        TimeSpan horizon,
        GraphThresholdPredictionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        options ??= new GraphThresholdPredictionOptions();
        Validate(samples, threshold, horizon, options);
        var ordered = samples.OrderBy(sample => sample.At).ToArray();
        if (ordered.Select(sample => sample.At).Distinct().Count() != ordered.Length)
        {
            throw new ArgumentException("Threshold prediction sample times must be unique.", nameof(samples));
        }

        var origin = ordered[0].At.ToUniversalTime();
        var x = new double[ordered.Length];
        var y = new double[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            x[index] = (ordered[index].At.ToUniversalTime() - origin).TotalSeconds;
            y[index] = ordered[index].Value;
        }

        var meanX = x.Average();
        var meanY = y.Average();
        var sumXX = x.Sum(value => (value - meanX) * (value - meanX));
        var sumXY = x.Zip(y).Sum(pair => (pair.First - meanX) * (pair.Second - meanY));
        var slope = sumXY / sumXX;
        var intercept = meanY - (slope * meanX);
        var residual = x.Zip(y).Sum(pair =>
        {
            var error = pair.Second - (intercept + (slope * pair.First));
            return error * error;
        });
        var total = y.Sum(value => (value - meanY) * (value - meanY));
        var rSquared = Math.Abs(total) < 1e-12 ? 1 : Math.Clamp(1 - (residual / total), 0, 1);
        var latestAt = ordered[^1].At.ToUniversalTime();
        var latestValue = ordered[^1].Value;
        var alreadyBreached = direction == GraphThresholdDirection.AtOrAbove ? latestValue >= threshold : latestValue <= threshold;
        var predictedAt = alreadyBreached ? latestAt : PredictFutureBreachAt(origin, latestAt, threshold, direction, slope, intercept);
        var withinHorizon = predictedAt is not null && predictedAt <= latestAt.Add(horizon);
        var explanation = alreadyBreached
            ? $"Latest value {latestValue:G17} already breaches threshold {threshold:G17}."
            : CreatePredictionExplanation(threshold, slope, rSquared, predictedAt, withinHorizon);
        return new GraphThresholdPrediction
        {
            SampleCount = ordered.Length,
            SlopePerSecond = slope,
            RSquared = rSquared,
            Threshold = threshold,
            Direction = direction,
            AlreadyBreached = alreadyBreached,
            PredictedBreachAt = predictedAt,
            WithinHorizon = withinHorizon,
            Explanation = explanation
        };
    }

    private static DateTimeOffset? PredictFutureBreachAt(
        DateTimeOffset origin,
        DateTimeOffset latestAt,
        double threshold,
        GraphThresholdDirection direction,
        double slope,
        double intercept)
    {
        var pointsToBreach = (direction == GraphThresholdDirection.AtOrAbove && slope > 0) || (direction == GraphThresholdDirection.AtOrBelow && slope < 0);
        if (!pointsToBreach)
        {
            return null;
        }

        var candidate = origin.AddSeconds((threshold - intercept) / slope);
        return candidate > latestAt ? candidate : null;
    }

    private static string CreatePredictionExplanation(double threshold, double slope, double rSquared, DateTimeOffset? predictedAt, bool withinHorizon)
    {
        if (predictedAt is null)
        {
            return $"Linear trend ({slope:G17}/second, R² {rSquared:F6}) does not point to a future breach.";
        }

        return $"Linear trend predicts threshold {threshold:G17} at {predictedAt:O}; within horizon: {withinHorizon}.";
    }

    private static void Validate(IReadOnlyCollection<GraphTwinMetricSample> samples, double threshold, TimeSpan horizon, GraphThresholdPredictionOptions options)
    {
        if (samples.Count < 2)
        {
            throw new ArgumentException("At least two threshold prediction samples are required.", nameof(samples));
        }

        if (options.MaximumSamples < 2 || samples.Count > options.MaximumSamples)
        {
            throw new InvalidOperationException($"Threshold prediction {nameof(samples)} exceed MaximumSamples ({options.MaximumSamples}).");
        }

        if (horizon <= TimeSpan.Zero || options.MaximumHorizon <= TimeSpan.Zero || horizon > options.MaximumHorizon)
        {
            throw new ArgumentOutOfRangeException(nameof(horizon));
        }

        if (!double.IsFinite(threshold) || samples.Any(sample => sample is null || !double.IsFinite(sample.Value)))
        {
            throw new ArgumentException("Threshold and sample values must be finite.");
        }
    }
}