namespace Gorm.Application.Execution;

/// <summary>
/// Defines graph query execution mode values.
/// </summary>
public enum GraphQueryExecutionMode
{
    /// <summary>
    /// Returns all matching results as a list.
    /// </summary>
    List = 0,

    /// <summary>
    /// Returns the first result; throws if there are no results.
    /// </summary>
    First = 1,

    /// <summary>
    /// Returns the first result, or <see langword="null"/> if there are no results.
    /// </summary>
    FirstOrDefault = 2,

    /// <summary>
    /// Returns the single result; throws if there is not exactly one result.
    /// </summary>
    Single = 3,

    /// <summary>
    /// Returns the single result, or <see langword="null"/> if there are no results; throws if there is more than one.
    /// </summary>
    SingleOrDefault = 4,

    /// <summary>
    /// Returns <see langword="true"/> if any results exist; otherwise <see langword="false"/>.
    /// </summary>
    Any = 5
}