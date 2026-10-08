namespace Gorm.Application.Mutations.Nodes;

/// <summary>
/// Represents the requested node mutation operation.
/// </summary>
public enum GraphMutationNodeOperation
{
    /// <summary>
    /// Adds the node as a new node.
    /// </summary>
    Add,

    /// <summary>
    /// Attaches the node as an existing unchanged node.
    /// </summary>
    Attach,

    /// <summary>
    /// Updates the node as an existing changed node.
    /// </summary>
    Update,

    /// <summary>
    /// Adds or updates the node based on its identity.
    /// </summary>
    Upsert,

    /// <summary>
    /// Removes the node.
    /// </summary>
    Remove
}