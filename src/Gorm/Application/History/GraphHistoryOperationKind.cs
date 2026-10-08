namespace Gorm.Application.History;

/// <summary>
/// Represents the kind of history operation.
/// </summary>
public enum GraphHistoryOperationKind
{
    /// <summary>
    /// The entity was created.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The entity was updated.
    /// </summary>
    Updated = 2,

    /// <summary>
    /// The entity was deleted.
    /// </summary>
    Deleted = 3,

    /// <summary>
    /// A relationship was connected.
    /// </summary>
    Connected = 4,

    /// <summary>
    /// A relationship was disconnected.
    /// </summary>
    Disconnected = 5
}