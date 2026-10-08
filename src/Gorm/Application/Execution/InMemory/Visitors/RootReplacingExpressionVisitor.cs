using System.Linq.Expressions;
using Gorm.Application.Querying.Models;

namespace Gorm.Application.Execution.InMemory.Visitors;

internal sealed class RootReplacingExpressionVisitor(GraphQueryRootExpression root, IQueryable replacement) : ExpressionVisitor
{
    public override Expression? Visit(Expression? node)
    {
        if (node is GraphQueryRootExpression rootExpression &&
            rootExpression.ElementType == root.ElementType &&
            rootExpression.ElementKind == root.ElementKind)
        {
            return Expression.Constant(replacement, node.Type);
        }

        return base.Visit(node);
    }
}