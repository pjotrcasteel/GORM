using System.Collections.ObjectModel;
using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Represents an immutable isolated what-if branch derived from a temporal world.
/// </summary>
public sealed class GraphScenario
{
    private readonly GraphScenarioMutation[] _mutations;

    private GraphScenario(GraphScenarioId id, GraphWorldSnapshot baseline, GraphWorldSnapshot current, GraphScenarioMutation[] mutations, GraphScenarioOptions options)
    {
        Id = id;
        Baseline = baseline;
        Current = current;
        _mutations = mutations;
        Mutations = Array.AsReadOnly(mutations);
        Options = options;
    }

    /// <summary>
    /// Gets the isolated scenario identifier.
    /// </summary>
    public GraphScenarioId Id { get; }

    /// <summary>
    /// Gets the unchanged source world from which this scenario was forked.
    /// </summary>
    public GraphWorldSnapshot Baseline { get; }

    /// <summary>
    /// Gets the current immutable scenario world.
    /// </summary>
    public GraphWorldSnapshot Current { get; }

    /// <summary>
    /// Gets the number of successfully applied scenario mutations.
    /// </summary>
    public int Revision => _mutations.Length;

    /// <summary>
    /// Gets detached applied mutations in revision order.
    /// </summary>
    public ReadOnlyCollection<GraphScenarioMutation> Mutations { get; }

    /// <summary>
    /// Gets immutable scenario safety options.
    /// </summary>
    public GraphScenarioOptions Options { get; }

    /// <summary>
    /// Forks an immutable world into an isolated scenario without changing the source world.
    /// </summary>
    /// <param name="baseline">Source world.</param>
    /// <param name="id">Scenario identifier.</param>
    /// <param name="options">Scenario growth and temporal-order safety options.</param>
    /// <returns>The zero-revision scenario branch.</returns>
    public static GraphScenario Fork(GraphWorldSnapshot baseline, GraphScenarioId id, GraphScenarioOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(id);
        options ??= new GraphScenarioOptions();
        ValidateOptions(options);
        ValidateSize(baseline, options);
        return new GraphScenario(id, baseline, baseline, [], options);
    }

    /// <summary>
    /// Applies one scenario-only mutation and returns a new immutable revision.
    /// </summary>
    /// <param name="mutation">Detached mutation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next scenario revision and exact difference.</returns>
    public GraphScenarioApplyResult Apply(GraphScenarioMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        if (Revision == Options.MaximumRevisions)
        {
            throw new GraphScenarioMutationException(
                GraphScenarioFailureReason.RevisionLimitExceeded,
                $"Scenario '{Id.Value}' reached MaximumRevisions ({Options.MaximumRevisions}).");
        }

        if (Options.RequireMonotonicRecordedTime && mutation.RecordedAt < Current.Identity.RecordedAt)
        {
            throw new GraphScenarioMutationException(
                GraphScenarioFailureReason.TemporalOrderViolation,
                $"Scenario {nameof(mutation)} recorded time {mutation.RecordedAt:O} precedes current world time " +
                $"{Current.Identity.RecordedAt:O}.");
        }

        var nodes = Current.Nodes.ToDictionary(state => state.Id, state => state.Materialize());
        var edges = Current.Edges.ToDictionary(state => state.Id, state => state.Materialize());
        var cascadedEdgeRemovals = mutation.EntityKind == GraphWorldEntityKind.Node ? ApplyNodeMutation(nodes, edges, mutation) : ApplyEdgeMutation(edges, mutation);

        GraphWorldSnapshot next;
        try
        {
            next = GraphWorldSnapshot.Capture(
                new GraphWorldSnapshot.CaptureParameters
                {
                    WorldKey = Current.Identity.WorldKey,
                    Version = Current.Identity.Version + 1,
                    ValidAt = mutation.ValidAt,
                    RecordedAt = mutation.RecordedAt,
                    Nodes = nodes.Values,
                    Edges = edges.Values,
                    CancellationToken = cancellationToken
                });
        }
        catch (ArgumentException exception)
        {
            throw new GraphScenarioMutationException(
                GraphScenarioFailureReason.InvalidTopology,
                $"Scenario {nameof(mutation)} '{mutation.Description}' produced invalid topology: {exception.Message}");
        }

        ValidateSize(next, Options);

        var nextMutations = new GraphScenarioMutation[_mutations.Length + 1];
        Array.Copy(_mutations, nextMutations, _mutations.Length);
        nextMutations[^1] = mutation;
        var scenario = new GraphScenario(Id, Baseline, next, nextMutations, Options);
        return new GraphScenarioApplyResult
        {
            Scenario = scenario,
            Difference = GraphWorldSnapshotDiffer.Compare(Current, next, cancellationToken: cancellationToken),
            CascadedEdgeRemovals = cascadedEdgeRemovals
        };
    }

    private static int ApplyNodeMutation(Dictionary<Guid, Node> nodes, IDictionary<Guid, Edge> edges, GraphScenarioMutation mutation)
    {
        var exists = nodes.ContainsKey(mutation.EntityId);
        ValidateExistence(exists, mutation);
        if (mutation.MutationKind == GraphScenarioMutationKind.Remove)
        {
            nodes.Remove(mutation.EntityId);
            var incidentEdgeIds = edges.Values.Where(edge => edge.FromId == mutation.EntityId || edge.ToId == mutation.EntityId).Select(edge => edge.Id).ToArray();
            foreach (var edgeId in incidentEdgeIds)
            {
                edges.Remove(edgeId);
            }

            return incidentEdgeIds.Length;
        }

        nodes[mutation.EntityId] = mutation.NodeState!.Materialize();
        return 0;
    }

    private static int ApplyEdgeMutation(Dictionary<Guid, Edge> edges, GraphScenarioMutation mutation)
    {
        var exists = edges.ContainsKey(mutation.EntityId);
        ValidateExistence(exists, mutation);
        if (mutation.MutationKind == GraphScenarioMutationKind.Remove)
        {
            edges.Remove(mutation.EntityId);
        }
        else
        {
            edges[mutation.EntityId] = mutation.EdgeState!.Materialize();
        }

        return 0;
    }

    private static void ValidateExistence(bool exists, GraphScenarioMutation mutation)
    {
        if (mutation.MutationKind == GraphScenarioMutationKind.Add && exists)
        {
            throw new GraphScenarioMutationException(
                GraphScenarioFailureReason.EntityAlreadyExists,
                $"Scenario entity '{mutation.EntityId}' already {nameof(exists)} and cannot be added again.");
        }

        if (mutation.MutationKind != GraphScenarioMutationKind.Add && !exists)
        {
            throw new GraphScenarioMutationException(
                GraphScenarioFailureReason.EntityNotFound,
                $"Scenario entity '{mutation.EntityId}' does not exist for {mutation.MutationKind}.");
        }
    }

    private static void ValidateOptions(GraphScenarioOptions options)
    {
        if (options.MaximumRevisions <= 0 || options.MaximumNodes <= 0 || options.MaximumEdges <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Scenario safety limits must be positive.");
        }
    }

    private static void ValidateSize(GraphWorldSnapshot snapshot, GraphScenarioOptions options)
    {
        if (snapshot.Nodes.Count > options.MaximumNodes || snapshot.Edges.Count > options.MaximumEdges)
        {
            throw new GraphScenarioMutationException(
                GraphScenarioFailureReason.SizeLimitExceeded,
                $"Scenario world contains {snapshot.Nodes.Count} nodes/{snapshot.Edges.Count} edges but allows at most " +
                $"{options.MaximumNodes}/{options.MaximumEdges}.");
        }
    }
}