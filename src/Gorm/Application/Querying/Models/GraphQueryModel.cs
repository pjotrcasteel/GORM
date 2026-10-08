namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph query model.
/// </summary>
public sealed class GraphQueryModel
{
    /// <summary>
    /// Gets the root CLR element type of the query.
    /// </summary>
    public required Type RootElementType { get; init; }

    /// <summary>
    /// Gets the root graph element kind of the query.
    /// </summary>
    public required GraphQueryElementKind RootElementKind { get; init; }

    /// <summary>
    /// Gets the translated query steps.
    /// </summary>
    public IList<GraphQueryStep> Steps { get; } = [];

    /// <summary>
    /// Gets the translated orderings.
    /// </summary>
    public IList<GraphOrdering> Orderings { get; } = [];

    /// <summary>
    /// Gets or sets the projection applied to the query.
    /// </summary>
    public GraphQueryProjection? Projection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether distinct results are requested.
    /// </summary>
    public bool IsDistinct { get; set; }

    /// <summary>
    /// Gets or sets the number of rows to skip.
    /// </summary>
    public int? SkipCount { get; set; }

    /// <summary>
    /// Gets or sets the number of rows to take.
    /// </summary>
    public int? TakeCount { get; set; }

    /// <summary>
    /// Gets track all.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphQueryTrackingMode TrackingMode { get; set; } = GraphQueryTrackingMode.TrackAll;

    /// <summary>
    /// Gets current element type.
    /// </summary>
    /// <returns>The value.</returns>
    public Type CurrentElementType
    {
        get
        {
            if (Steps.Count == 0)
            {
                return RootElementType;
            }

            return Steps[^1].OutputElementType;
        }
    }

    /// <summary>
    /// Gets current element kind.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphQueryElementKind CurrentElementKind
    {
        get
        {
            if (Steps.Count == 0)
            {
                return RootElementKind;
            }

            return Steps[^1].OutputElementKind;
        }
    }

    /// <summary>
    /// Executes clone.
    /// </summary>
    /// <returns>The value.</returns>
    internal GraphQueryModel Clone()
    {
        var clone = new GraphQueryModel
        {
            RootElementType = RootElementType,
            RootElementKind = RootElementKind,
            Projection = Projection,
            IsDistinct = IsDistinct,
            SkipCount = SkipCount,
            TakeCount = TakeCount,
            TrackingMode = TrackingMode
        };

        foreach (var step in Steps)
        {
            clone.Steps.Add(step);
        }

        foreach (var ordering in Orderings)
        {
            clone.Orderings.Add(new GraphOrdering
            {
                PropertyName = ordering.PropertyName,
                Descending = ordering.Descending,
                IsProjected = ordering.IsProjected
            });
        }

        return clone;
    }
}