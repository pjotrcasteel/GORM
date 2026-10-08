namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Identifies one isolated what-if scenario branch.
/// </summary>
public sealed record GraphScenarioId
{
    /// <summary>
    /// Initializes a scenario identifier.
    /// </summary>
    /// <param name="value">Stable application-defined identifier.</param>
    public GraphScenarioId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A scenario identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the stable identifier.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}