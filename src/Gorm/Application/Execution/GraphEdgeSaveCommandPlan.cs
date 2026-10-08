namespace Gorm.Application.Execution;

/// <summary>
/// Represents a cached edge save command plan.
/// </summary>
internal sealed class GraphEdgeSaveCommandPlan : SaveCommandPlanBase
{
    public required string[] DeleteByNodeIdsParameterNames { get; init; }
    public required string DeleteByNodeIdsCommandText { get; init; }
}