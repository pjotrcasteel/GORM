namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Identifies a scenario-only graph mutation operation.
/// </summary>
public enum GraphScenarioMutationKind
{
    /// <summary>
    /// Add a new entity.
    /// </summary>
    Add,

    /// <summary>
    /// Replace the captured state of an existing entity.
    /// </summary>
    Update,

    /// <summary>
    /// Remove an existing entity.
    /// </summary>
    Remove
}