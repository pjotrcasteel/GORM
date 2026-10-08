namespace Gorm.Application.Temporal.Diff;

/// <summary>
/// Identifies how an entity changed between temporal worlds.
/// </summary>
public enum GraphWorldChangeKind
{
    /// <summary>
    /// The entity exists only in the newer world.
    /// </summary>
    Added,

    /// <summary>
    /// The entity exists only in the older world.
    /// </summary>
    Removed,

    /// <summary>
    /// The entity exists in both worlds but its captured state changed.
    /// </summary>
    Modified
}