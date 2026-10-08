namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Describes how a projection update was produced.
/// </summary>
public enum GraphProjectionUpdateMode
{
    /// <summary>
    /// The compatible delta was applied to the previous immutable snapshot.
    /// </summary>
    Incremental,

    /// <summary>
    /// An explicit full-rebuild factory produced a safe current snapshot.
    /// </summary>
    FullRebuild
}