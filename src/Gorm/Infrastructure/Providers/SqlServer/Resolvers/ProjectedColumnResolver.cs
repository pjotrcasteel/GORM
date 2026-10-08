using Gorm.Application.Querying.Models;

namespace Gorm.Infrastructure.Providers.SqlServer.Resolvers;

/// <summary>
/// Represents projected column resolver.
/// </summary>
public sealed class ProjectedColumnResolver
{
    private readonly Dictionary<string, Type> _columns;
    private readonly string? _scalarColumnName;
    private readonly Type? _scalarType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectedColumnResolver"/> class.
    /// </summary>
    /// <param name="scalarColumnName">The scalar column name.</param>
    /// <param name="scalarType">The scalar type.</param>
    public ProjectedColumnResolver(string scalarColumnName, Type scalarType)
    {
        _scalarColumnName = scalarColumnName;
        _scalarType = scalarType;
        _columns = new Dictionary<string, Type>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectedColumnResolver"/> class.
    /// </summary>
    /// <param name="columns">The columns.</param>
    public ProjectedColumnResolver(Dictionary<string, Type> columns)
    {
        _columns = columns;
    }

    /// <summary>
    /// Executes try resolve scalar.
    /// </summary>
    /// <param name="columnName">The column name.</param>
    /// <param name="columnType">The column type.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryResolveScalar(out string columnName, out Type? columnType)
    {
        if (_scalarColumnName is not null)
        {
            columnName = _scalarColumnName;
            columnType = _scalarType;
            return true;
        }

        columnName = string.Empty;
        columnType = null;
        return false;
    }

    /// <summary>
    /// Executes try resolve member.
    /// </summary>
    /// <param name="memberName">The member name.</param>
    /// <param name="columnName">The column name.</param>
    /// <param name="columnType">The column type.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryResolveMember(string memberName, out string columnName, out Type? columnType)
    {
        if (_columns.TryGetValue(memberName, out var type))
        {
            columnName = memberName;
            columnType = type;
            return true;
        }

        columnName = string.Empty;
        columnType = null;
        return false;
    }

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public static ProjectedColumnResolver CreateProjectedColumnResolver(GraphQueryModel queryModel) => queryModel.Projection switch
    {
        GraphScalarProjection => new ProjectedColumnResolver("Value", typeof(object)),
        GraphObjectProjection objectProjection => new ProjectedColumnResolver(
            objectProjection.Bindings.ToDictionary(x => x.TargetMemberName, x => typeof(object), StringComparer.Ordinal)),
        GraphConstructorProjection constructorProjection => new ProjectedColumnResolver(
            constructorProjection.Parameters.ToDictionary(x => x.ParameterName, x => x.ParameterType, StringComparer.Ordinal)),
        _ => throw new NotSupportedException("Projected filtering requires a supported projection.")
    };
}