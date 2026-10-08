namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Identifies the logical source and scope of a reusable graph projection.
/// </summary>
public sealed record GraphProjectionKey
{
    /// <summary>
    /// Initializes a projection key.
    /// </summary>
    /// <param name="value">A stable application-defined source and scope identifier.</param>
    public GraphProjectionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A graph projection key cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the stable application-defined key value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}