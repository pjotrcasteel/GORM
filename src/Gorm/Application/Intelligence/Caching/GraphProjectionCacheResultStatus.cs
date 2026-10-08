namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Describes how a projection cache result was obtained.
/// </summary>
public enum GraphProjectionCacheResultStatus
{
    /// <summary>
    /// An unexpired cached snapshot satisfied the requested minimum version.
    /// </summary>
    Hit,

    /// <summary>
    /// No prior snapshot existed and the load factory created one.
    /// </summary>
    Loaded,

    /// <summary>
    /// An expired or insufficient snapshot was replaced by the load factory.
    /// </summary>
    Refreshed
}