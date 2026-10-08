using System.Text;
using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;
using Gorm.Infrastructure.Sql;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Represents sql generation context.
/// </summary>
public sealed class SqlGenerationContext
{
    private int _aliasCounter;
    private int _parameterCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlGenerationContext"/> class.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="rootElementType">The root element type.</param>
    /// <param name="rootElementKind">The root element kind.</param>
    public SqlGenerationContext(GraphModel model, Type rootElementType, GraphQueryElementKind rootElementKind)
    {
        Model = model;
        CurrentElementType = rootElementType;
        CurrentElementKind = rootElementKind;
        CurrentAlias = NextAlias(rootElementKind == GraphQueryElementKind.Node ? "n" : "e");

        switch (rootElementKind)
        {
            case GraphQueryElementKind.Node:
                AddFromItem(GetNode(rootElementType), CurrentAlias);
                break;

            case GraphQueryElementKind.Edge:
                AddFromItem(GetEdge(rootElementType), CurrentAlias);
                break;

            default:
                throw new NotSupportedException($"Element kind '{rootElementKind}' is not supported.");
        }
    }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public GraphModel Model { get; }

    /// <summary>
    /// Gets or sets the current element type.
    /// </summary>
    public Type CurrentElementType { get; set; }

    /// <summary>
    /// Gets or sets the current element kind.
    /// </summary>
    public GraphQueryElementKind CurrentElementKind { get; set; }

    /// <summary>
    /// Gets or sets the current alias.
    /// </summary>
    public string CurrentAlias { get; set; }

    /// <summary>
    /// Gets or sets the last traversal edge alias.
    /// </summary>
    public string? LastTraversalEdgeAlias { get; set; }

    /// <summary>
    /// Gets or sets the last traversal edge type.
    /// </summary>
    public Type? LastTraversalEdgeType { get; set; }

    /// <summary>
    /// Gets or sets the source node alias of the last traversed edge.
    /// </summary>
    public string? LastTraversalFromNodeAlias { get; set; }

    /// <summary>
    /// Gets or sets the destination node alias of the last traversed edge.
    /// </summary>
    public string? LastTraversalToNodeAlias { get; set; }

    /// <summary>
    /// Gets or sets the from items.
    /// </summary>
    public IList<string> FromItems { get; } = new List<string>(capacity: 4);

    /// <summary>
    /// Gets or sets the match predicates.
    /// </summary>
    public IList<string> MatchPredicates { get; } = new List<string>(capacity: 4);

    /// <summary>
    /// Gets or sets the where predicates.
    /// </summary>
    public IList<string> WherePredicates { get; } = new List<string>(capacity: 4);

    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    public IList<GraphSqlParameter> Parameters { get; } = new List<GraphSqlParameter>(capacity: 8);

    /// <summary>
    /// Executes next alias.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The value.</returns>
    public string NextAlias(string prefix)
    {
        var alias = string.Concat(prefix, _aliasCounter);
        _aliasCounter++;

        return alias;
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    public string AddParameter(object? value)
    {
        var name = string.Concat("@p", _parameterCounter);
        _parameterCounter++;

        Parameters.Add(GraphSqlParameter.Create(name, value));

        return name;
    }

    /// <summary>
    /// Gets node mapping.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The value.</returns>
    public NodeTypeMapping GetNode(Type clrType) =>
        Model.GetNode(clrType);

    /// <summary>
    /// Gets edge mapping.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The value.</returns>
    public EdgeTypeMapping GetEdge(Type clrType) =>
        Model.GetEdge(clrType);

    /// <summary>
    /// Adds from item.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="alias">The alias.</param>
    public void AddFromItem(NodeTypeMapping mapping, string alias) =>
        FromItems.Add($"{SqlGenerationHelpers.EscapeFullName(mapping.Schema, mapping.TableName)} AS {alias}");

    /// <summary>
    /// Adds from item.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="alias">The alias.</param>
    public void AddFromItem(EdgeTypeMapping mapping, string alias) =>
        FromItems.Add($"{SqlGenerationHelpers.EscapeFullName(mapping.Schema, mapping.TableName)} AS {alias}");

    /// <summary>
    /// Appends from items.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public void AppendFromItems(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        for (var i = 0; i < FromItems.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine(",");
                builder.Append("     ");
            }

            builder.Append(FromItems[i]);
        }

        builder.AppendLine();
    }

    /// <summary>
    /// Appends predicates.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public void AppendWhere(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (MatchPredicates.Count == 0 && WherePredicates.Count == 0)
        {
            return;
        }

        builder.Append("WHERE ");

        var hasPrevious = false;

        AppendPredicateGroup(builder, MatchPredicates, ref hasPrevious);
        AppendPredicateGroup(builder, WherePredicates, ref hasPrevious);

        builder.AppendLine();
    }

    /// <summary>
    /// Gets current key property name.
    /// </summary>
    /// <returns>The value.</returns>
    public string GetCurrentKeyPropertyName => GetKeyPropertyName(CurrentElementType, CurrentElementKind);

    /// <summary>
    /// Gets key property name.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="elementKind">The element kind.</param>
    /// <returns>The value.</returns>
    public string GetKeyPropertyName(Type elementType, GraphQueryElementKind elementKind) =>
        elementKind switch
        {
            GraphQueryElementKind.Node => GetNode(elementType).KeyPropertyName,
            GraphQueryElementKind.Edge => GetEdge(elementType).KeyPropertyName,
            _ => throw new NotSupportedException($"Element kind '{elementKind}' is not supported.")
        };

    /// <summary>
    /// Gets projection alias.
    /// </summary>
    /// <param name="sourceKind">The source kind.</param>
    /// <returns>The value.</returns>
    public string GetProjectionAlias(GraphProjectionSourceKind sourceKind) =>
        sourceKind switch
        {
            GraphProjectionSourceKind.Node => CurrentAlias,
            GraphProjectionSourceKind.Edge when LastTraversalEdgeAlias is not null => LastTraversalEdgeAlias,
            GraphProjectionSourceKind.Edge => throw new NotSupportedException("Edge projection requires a preceding traversal step."),
            _ => throw new NotSupportedException($"Projection source kind '{sourceKind}' is not supported.")
        };

    /// <summary>
    /// Gets projection property names.
    /// </summary>
    /// <param name="sourceKind">The source kind.</param>
    /// <returns>The value.</returns>
    public string[] GetProjectionPropertyNames(GraphProjectionSourceKind sourceKind)
    {
        var elementType = sourceKind switch
        {
            GraphProjectionSourceKind.Node => CurrentElementType,
            GraphProjectionSourceKind.Edge when LastTraversalEdgeType is not null => LastTraversalEdgeType,
            GraphProjectionSourceKind.Edge => throw new NotSupportedException("Edge projection requires a preceding traversal step."),
            _ => throw new NotSupportedException($"Projection source kind '{sourceKind}' is not supported.")
        };

        return sourceKind switch
        {
            GraphProjectionSourceKind.Node => Model.GetNodeProjectionPropertyNames(elementType),
            GraphProjectionSourceKind.Edge => Model.GetEdgeProjectionPropertyNames(elementType),
            _ => throw new NotSupportedException($"Projection source kind '{sourceKind}' is not supported.")
        };
    }

    private static void AppendPredicateGroup(StringBuilder builder, IList<string> predicates, ref bool hasPrevious)
    {
        for (var i = 0; i < predicates.Count; i++)
        {
            if (hasPrevious)
            {
                builder.AppendLine();
                builder.Append("  AND ");
            }

            builder.Append(predicates[i]);
            hasPrevious = true;
        }
    }
}