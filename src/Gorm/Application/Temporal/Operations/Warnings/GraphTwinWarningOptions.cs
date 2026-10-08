namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Bounds early-warning evaluation.
/// </summary>
public sealed class GraphTwinWarningOptions
{
    /// <summary>
    /// Gets or sets the maximum rule count.
    /// </summary>
    public int MaximumRules { get; init; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum emitted warning count.
    /// </summary>
    public int MaximumWarnings { get; init; } = 10_000;
}