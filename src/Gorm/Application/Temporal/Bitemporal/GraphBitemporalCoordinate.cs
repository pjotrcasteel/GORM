namespace Gorm.Application.Temporal.Bitemporal;

/// <summary>
/// Identifies one business-valid instant and one recorded-knowledge cutoff.
/// </summary>
public readonly record struct GraphBitemporalCoordinate
{
    /// <summary>
    /// Initializes a normalized UTC bitemporal coordinate.
    /// </summary>
    /// <param name="validAt">Business-valid instant.</param>
    /// <param name="recordedAt">Recorded-knowledge cutoff.</param>
    public GraphBitemporalCoordinate(DateTimeOffset validAt, DateTimeOffset recordedAt)
    {
        ValidAt = validAt.ToUniversalTime();
        RecordedAt = recordedAt.ToUniversalTime();
    }

    /// <summary>
    /// Gets the normalized UTC business-valid instant.
    /// </summary>
    public DateTimeOffset ValidAt { get; }

    /// <summary>
    /// Gets the normalized UTC recorded-knowledge cutoff.
    /// </summary>
    public DateTimeOffset RecordedAt { get; }
}