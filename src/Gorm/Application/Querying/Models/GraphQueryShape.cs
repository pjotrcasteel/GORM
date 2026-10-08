namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph query shape.
/// </summary>
public sealed class GraphQueryShape
{
    private readonly List<GraphOrdering> _orderings = [];

    /// <summary>
    /// Gets orderings.
    /// </summary>
    /// <returns>The items.</returns>
    public IReadOnlyList<GraphOrdering> Orderings => _orderings;
    /// <summary>
    /// Gets the maximum number of rows to return, or <see langword="null"/> if unbounded.
    /// </summary>
    public int? Take { get; private set; }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    public void AddOrdering(GraphOrdering ordering) => _orderings.Add(ordering);

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="take">The take.</param>
    public void SetTake(int take)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be greater than zero.");
        }

        Take = take;
    }
}