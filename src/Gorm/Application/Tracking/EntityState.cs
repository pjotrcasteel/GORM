namespace Gorm.Application.Tracking;

/// <summary>
/// Defines entity state values.
/// </summary>
public enum EntityState
{
    ///<inheritdoc/>
    Detached = 0,
    ///<inheritdoc/>
    Unchanged = 1,
    ///<inheritdoc/>
    Added = 2,
    ///<inheritdoc/>
    Modified = 3,
    ///<inheritdoc/>
    Deleted = 4
}