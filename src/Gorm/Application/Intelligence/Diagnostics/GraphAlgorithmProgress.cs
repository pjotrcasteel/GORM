namespace Gorm.Application.Intelligence.Diagnostics;

/// <summary>
/// Represents deterministic progress reported at a stable algorithm boundary.
/// </summary>
public sealed class GraphAlgorithmProgress
{
    /// <summary>
    /// Gets the stable operation name.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the current algorithm stage.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// Gets the completed unit count.
    /// </summary>
    public required long Completed { get; init; }

    /// <summary>
    /// Gets the total unit count when it is known.
    /// </summary>
    public required long? Total { get; init; }

    /// <summary>
    /// Gets a human-readable progress explanation.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the completion ratio when a positive total is known.
    /// </summary>
    public double? Fraction => Total is > 0 ? Math.Min(Completed / (double)Total.Value, 1) : null;
}