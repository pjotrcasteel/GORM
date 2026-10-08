using System.Linq.Expressions;
using Gorm.Application.Querying.Models;
using Gorm.Core.Primitives;

namespace Gorm.Core.Paths;
/// <summary>
/// Represents graph path.
/// </summary>
public sealed class GraphPath<TRoot, TCurrent>
    where TRoot : Node
    where TCurrent : Node
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphPath"/> class.
    /// </summary>
    /// <param name="steps">The steps.</param>
    /// <param name="distinctResults">The distinct results.</param>
    internal GraphPath(IReadOnlyList<GraphPathStep> steps, bool distinctResults)
    {
        Steps = steps;
        DistinctResultsEnabled = distinctResults;
    }

    /// <summary>
    /// Gets or sets the steps.
    /// </summary>
    internal IReadOnlyList<GraphPathStep> Steps { get; }

    /// <summary>
    /// Gets or sets the distinct results enabled.
    /// </summary>
    internal bool DistinctResultsEnabled { get; }

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Outgoing<TEdge, TNext>()
        where TEdge : Edge
        where TNext : Node => Outgoing<TEdge, TNext>(edgePredicate: null, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Outgoing<TEdge, TNext>(GraphTraversalSafeties safety)
        where TEdge : Edge
        where TNext : Node => Outgoing<TEdge, TNext>(edgePredicate: null, safety);

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Outgoing<TEdge, TNext>(Expression<Func<TEdge, bool>>? edgePredicate)
        where TEdge : Edge
        where TNext : Node => Outgoing<TEdge, TNext>(edgePredicate, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Outgoing<TEdge, TNext>(Expression<Func<TEdge, bool>>? edgePredicate, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TNext : Node => Append<TNext>(new GraphPathStep
        {
            Direction = GraphPathDirection.Outgoing,
            EdgeType = typeof(TEdge),
            NodeType = typeof(TNext),
            EdgePredicate = edgePredicate,
            Safety = safety
        });

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Incoming<TEdge, TNext>()
        where TEdge : Edge
        where TNext : Node => Incoming<TEdge, TNext>(edgePredicate: null, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Incoming<TEdge, TNext>(GraphTraversalSafeties safety)
        where TEdge : Edge
        where TNext : Node => Incoming<TEdge, TNext>(edgePredicate: null, safety);

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Incoming<TEdge, TNext>(Expression<Func<TEdge, bool>>? edgePredicate)
        where TEdge : Edge
        where TNext : Node => Incoming<TEdge, TNext>(edgePredicate, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNext">The type of t next.</typeparam>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TNext> Incoming<TEdge, TNext>(Expression<Func<TEdge, bool>>? edgePredicate, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TNext : Node => Append<TNext>(new GraphPathStep
        {
            Direction = GraphPathDirection.Incoming,
            EdgeType = typeof(TEdge),
            NodeType = typeof(TNext),
            EdgePredicate = edgePredicate,
            Safety = safety
        });

    /// <summary>
    /// Executes repeat outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatOutgoing<TEdge>(int hopCount)
        where TEdge : Edge => RepeatOutgoing<TEdge>(hopCount, edgePredicate: null, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes repeat outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatOutgoing<TEdge>(int hopCount, GraphTraversalSafeties safety)
        where TEdge : Edge => RepeatOutgoing<TEdge>(hopCount, edgePredicate: null, safety);

    /// <summary>
    /// Executes repeat outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatOutgoing<TEdge>(int hopCount, Expression<Func<TEdge, bool>>? edgePredicate)
        where TEdge : Edge => RepeatOutgoing<TEdge>(hopCount, edgePredicate, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes repeat outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatOutgoing<TEdge>(int hopCount, Expression<Func<TEdge, bool>>? edgePredicate, GraphTraversalSafeties safety)
        where TEdge : Edge
    {
        if (hopCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hopCount), "Hop count must be greater than zero.");
        }

        var path = this;

        for (var i = 0; i < hopCount; i++)
        {
            path = path.Outgoing<TEdge, TCurrent>(edgePredicate, safety);
        }

        return path;
    }

    /// <summary>
    /// Executes repeat incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatIncoming<TEdge>(int hopCount)
        where TEdge : Edge => RepeatIncoming<TEdge>(hopCount, edgePredicate: null, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes repeat incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatIncoming<TEdge>(int hopCount, GraphTraversalSafeties safety)
        where TEdge : Edge => RepeatIncoming<TEdge>(hopCount, edgePredicate: null, safety);

    /// <summary>
    /// Executes repeat incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatIncoming<TEdge>(int hopCount, Expression<Func<TEdge, bool>>? edgePredicate)
        where TEdge : Edge => RepeatIncoming<TEdge>(hopCount, edgePredicate, GraphTraversalSafeties.None);

    /// <summary>
    /// Executes repeat incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="hopCount">The hop count.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> RepeatIncoming<TEdge>(int hopCount, Expression<Func<TEdge, bool>>? edgePredicate, GraphTraversalSafeties safety)
        where TEdge : Edge
    {
        if (hopCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hopCount), "Hop count must be greater than zero.");
        }

        var path = this;

        for (var i = 0; i < hopCount; i++)
        {
            path = path.Incoming<TEdge, TCurrent>(edgePredicate, safety);
        }

        return path;
    }

    /// <summary>
    /// Executes guard against immediate cycles.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> GuardAgainstImmediateCycles() => UpdateLastStep(step => step with
    {
        Safety = step.Safety | GraphTraversalSafeties.PreventImmediateCycles
    });

    /// <summary>
    /// Executes guard against node revisit.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> GuardAgainstNodeRevisit() => UpdateLastStep(step => step with
    {
        Safety = step.Safety | GraphTraversalSafeties.PreventNodeRevisit
    });

    /// <summary>
    /// Executes distinct results.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphPath<TRoot, TCurrent> DistinctResults() => new(Steps, distinctResults: true);

    private GraphPath<TRoot, TNext> Append<TNext>(GraphPathStep step)
        where TNext : Node
    {
        var next = Steps.ToList();
        next.Add(step);
        return new GraphPath<TRoot, TNext>(next, DistinctResultsEnabled);
    }

    private GraphPath<TRoot, TCurrent> UpdateLastStep(Func<GraphPathStep, GraphPathStep> update)
    {
        if (Steps.Count == 0)
        {
            throw new InvalidOperationException("A cycle guard requires at least one path step.");
        }

        var next = Steps.ToList();
        next[^1] = update(next[^1]);
        return new GraphPath<TRoot, TCurrent>(next, DistinctResultsEnabled);
    }
}