namespace Gorm.Application.Querying.Models;

/// <summary>
/// Defines graph traversal safety values.
/// </summary>
[Flags]
public enum GraphTraversalSafeties
{
    ///<inheritdoc/>
    None = 0,
    ///<inheritdoc/>
    PreventImmediateCycles = 1,
    ///<inheritdoc/>
    PreventNodeRevisit = 2
}