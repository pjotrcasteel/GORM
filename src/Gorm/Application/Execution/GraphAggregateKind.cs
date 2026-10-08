namespace Gorm.Application.Execution;

/// <summary>
/// Defines graph aggregate kind values.
/// </summary>
public enum GraphAggregateKind
{
    /// <summary>
    /// Computes the sum of values.
    /// </summary>
    Sum = 0,

    /// <summary>
    /// Computes the average of values.
    /// </summary>
    Average = 1,

    /// <summary>
    /// Returns the minimum value.
    /// </summary>
    Min = 2,

    /// <summary>
    /// Returns the maximum value.
    /// </summary>
    Max = 3
}