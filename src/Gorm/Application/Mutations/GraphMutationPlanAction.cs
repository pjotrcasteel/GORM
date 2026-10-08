namespace Gorm.Application.Mutations;

/// <summary>
/// Represents the concrete action GORM will execute for a mutation item.
/// </summary>
public enum GraphMutationPlanAction
{
    /// <summary>
    /// Inserts the item.
    /// </summary>
    Insert,

    /// <summary>
    /// Attaches the item as unchanged.
    /// </summary>
    Attach,

    /// <summary>
    /// Updates the item.
    /// </summary>
    Update,

    /// <summary>
    /// Deletes the item.
    /// </summary>
    Delete,

    /// <summary>
    /// Does nothing.
    /// </summary>
    NoOp
}