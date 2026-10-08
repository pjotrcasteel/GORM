namespace Gorm.Application.Intelligence.Output;

/// <summary>
/// Confirms one explicitly completed output adapter invocation.
/// </summary>
public sealed class GraphOutputDispatchReceipt
{
    /// <summary>
    /// Gets the adapter identifier.
    /// </summary>
    public required string AdapterId { get; init; }

    /// <summary>
    /// Gets the UTC dispatch start.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets the UTC dispatch completion.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Gets elapsed adapter execution time.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}