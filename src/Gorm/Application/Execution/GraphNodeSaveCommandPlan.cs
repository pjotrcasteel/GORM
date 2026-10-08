namespace Gorm.Application.Execution;

/// <summary>
/// Represents a cached node save command plan.
/// </summary>
internal sealed class GraphNodeSaveCommandPlan : SaveCommandPlanBase
{
    public required string[] LoadNodeIdParameterNames { get; init; }
    public required string LoadNodeIdCommandText { get; init; }
}