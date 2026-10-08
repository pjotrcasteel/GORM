using System.Linq.Expressions;
using Gorm.Application.Querying.Models;

namespace Gorm.Application.Execution.InMemory.Visitors;

internal sealed class RootFindingExpressionVisitor : ExpressionVisitor
{
    public GraphQueryRootExpression? Root { get; private set; }

    public override Expression? Visit(Expression? node)
    {
        if (node is GraphQueryRootExpression rootExpression)
        {
            Root ??= rootExpression;
            return node;
        }

        return base.Visit(node);
    }
}