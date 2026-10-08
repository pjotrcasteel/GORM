using Gorm.Application.Mutations.Edges;
using Gorm.Application.Mutations.Nodes;
using Gorm.Application.Mutations.Validations;

namespace Gorm.Application.Mutations;

/// <summary>
/// Represents a planned graph mutation.
/// </summary>
public sealed class GraphMutationPlan
{
    /// <summary>
    /// Gets or sets the mutation name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the optional correlation id.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the optional source event id.
    /// </summary>
    public string? SourceEventId { get; init; }

    /// <summary>
    /// Gets or sets the validation result.
    /// </summary>
    public required GraphMutationValidationResult Validation { get; init; }

    /// <summary>
    /// Gets or sets the planned node items.
    /// </summary>
    public IReadOnlyList<GraphMutationNodePlanItem> Nodes { get; init; } = [];

    /// <summary>
    /// Gets or sets the planned edge items.
    /// </summary>
    public IReadOnlyList<GraphMutationEdgePlanItem> Edges { get; init; } = [];

    /// <summary>
    /// Creates a readable debug view.
    /// </summary>
    /// <returns>The debug view.</returns>
    public string ToDebugString() =>
        GraphMutationDebugView.Format(this);
}