namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph query execution request.
/// </summary>
public sealed class GraphQueryExecutionRequest
{
    /// <summary>
    /// Gets the mode that controls how results are fetched and shaped.
    /// </summary>
    public required GraphQueryExecutionMode Mode { get; init; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="queryTake">The query take.</param>
    /// <returns>The value.</returns>
    public int? GetEffectiveTake(int? queryTake)
    {
        int? modeTake = Mode switch
        {
            GraphQueryExecutionMode.First => 1,
            GraphQueryExecutionMode.FirstOrDefault => 1,
            GraphQueryExecutionMode.Single => 2,
            GraphQueryExecutionMode.SingleOrDefault => 2,
            GraphQueryExecutionMode.Any => 1,
            GraphQueryExecutionMode.List => null,
            _ => null
        };

        if (modeTake is null)
        {
            return queryTake;
        }

        if (queryTake is null)
        {
            return modeTake;
        }

        return Math.Min(modeTake.Value, queryTake.Value);
    }

    /// <summary>
    /// Gets any.
    /// </summary>
    /// <returns>The value.</returns>
    public bool IsExistenceOnly => Mode == GraphQueryExecutionMode.Any;
}