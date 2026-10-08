namespace Gorm.Application.Querying.Models;

/// <summary>
/// Defines graph terminal operator kind values.
/// </summary>
public enum GraphTerminalOperatorKind
{
    ///<inheritdoc/>
    None = 0,
    ///<inheritdoc/>
    ToList = 1,
    ///<inheritdoc/>
    First = 2,
    ///<inheritdoc/>
    FirstOrDefault = 3,
    ///<inheritdoc/>
    Single = 4,
    ///<inheritdoc/>
    SingleOrDefault = 5,
    ///<inheritdoc/>
    Any = 6
}