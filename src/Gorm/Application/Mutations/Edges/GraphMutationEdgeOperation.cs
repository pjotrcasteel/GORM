namespace Gorm.Application.Mutations.Edges;

/// <summary>
/// Represents the requested edge mutation operation.
/// </summary>
public enum GraphMutationEdgeOperation
{
    /// <summary>
    /// Adds the edge as a new edge.
    /// </summary>
    Add,

    /// <summary>
    /// Adds or updates the edge based on its identity.
    /// </summary>
    Upsert,

    /// <summary>
    /// Removes the edge between the source and target nodes.
    /// </summary>
    Remove
}