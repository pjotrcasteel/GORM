using System.Linq.Expressions;

namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph query root expression.
/// </summary>
internal sealed class GraphQueryRootExpression : Expression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryRootExpression"/> class.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="elementKind">The element kind.</param>
    public GraphQueryRootExpression(Type elementType, GraphQueryElementKind elementKind)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        ElementKind = elementKind;
    }

    /// <summary>
    /// Gets or sets the element type.
    /// </summary>
    public Type ElementType { get; }

    /// <summary>
    /// Gets or sets the element kind.
    /// </summary>
    public GraphQueryElementKind ElementKind { get; }

    /// <summary>
    /// Gets extension.
    /// </summary>
    /// <returns>The value.</returns>
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>
    /// Executes typeof.
    /// </summary>
    /// <returns>The value.</returns>
    public override Type Type => typeof(IQueryable<>).MakeGenericType(ElementType);

    /// <summary>
    /// Executes to string.
    /// </summary>
    /// <returns>The value.</returns>
    public override string ToString() => $"GraphQueryRoot<{ElementType.Name}>[{ElementKind}]";
}