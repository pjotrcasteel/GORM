namespace Gorm.Application.Intelligence.Algorithms.Communities;

internal static class GraphCommunityDetectionOptionsValidator
{
    public static void Validate(GraphCommunityDetectionOptions options)
    {
        if (!Enum.IsDefined(options.EdgeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.EdgeMode, "Unknown community edge mode.");
        }

        if (!double.IsFinite(options.Resolution) || options.Resolution <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Resolution must be finite and greater than zero.");
        }

        if (options.MaximumLevels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum levels must be greater than zero.");
        }

        if (options.MaximumLocalMovingPasses <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum local-moving passes must be greater than zero.");
        }

        if (!double.IsFinite(options.MinimumModularityGain) || options.MinimumModularityGain < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum modularity gain must be finite and non-negative.");
        }

        if (!double.IsFinite(options.AnomalyThreshold) ||
            options.AnomalyThreshold < 0 ||
            options.AnomalyThreshold > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Anomaly threshold must be between zero and one.");
        }
    }
}