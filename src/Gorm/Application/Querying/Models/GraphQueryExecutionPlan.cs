namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph query execution plan.
/// </summary>
public sealed class GraphQueryExecutionPlan
{
    /// <summary>
    /// Gets to list.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphTerminalOperatorKind TerminalOperator { get; init; } = GraphTerminalOperatorKind.ToList;

    /// <summary>
    /// Gets effective row limit.
    /// </summary>
    /// <returns>The value.</returns>
    public int? EffectiveRowLimit
    {
        get
        {
            return TerminalOperator switch
            {
                GraphTerminalOperatorKind.First => 1,
                GraphTerminalOperatorKind.FirstOrDefault => 1,
                GraphTerminalOperatorKind.Single => 2,
                GraphTerminalOperatorKind.SingleOrDefault => 2,
                _ => null
            };
        }
    }

    /// <summary>
    /// Gets any.
    /// </summary>
    /// <returns>The value.</returns>
    public bool IsExistenceCheck => TerminalOperator == GraphTerminalOperatorKind.Any;
}