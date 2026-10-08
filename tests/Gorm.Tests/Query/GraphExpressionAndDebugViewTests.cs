using System.Linq.Expressions;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Diagnostics;
using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphExpressionAndDebugViewTests
{
    [TestMethod]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0041:Use 'is null' check", Justification = "is not null cannot be used in a lambda")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1866:Use char overload", Justification = "'Z' != \"Z\"")]
    public void Format_renders_readable_lambda_text_for_common_expression_shapes()
    {
        var person = new TestPerson { Name = "Alice", Age = 35 };
        Expression<Func<TestPerson, bool>> expression = x => x.Name == person.Name && x.Age >= 18 && !x.Name.StartsWith("Z") && (object)x.Age != null;

        var result = GraphExpressionDebugFormatter.Format(expression);

        Assert.Contains("x =>", result);
        Assert.Contains("x.Name == \"Alice\"", result);
        Assert.Contains("x.Age >= 18", result);
        Assert.Contains("!x.Name.StartsWith(\"Z\")", result);
        Assert.Contains("x.Age != null", result);
    }

    [TestMethod]
    public void Format_handles_constants_chars_booleans_and_null()
    {
        Expression<Func<string?>> expression = () => null;
        Expression<Func<char>> charExpression = () => 'x';
        Expression<Func<bool>> boolExpression = () => true;

        Assert.AreEqual(" => null", GraphExpressionDebugFormatter.Format(expression));
        Assert.AreEqual(" => 'x'", GraphExpressionDebugFormatter.Format(charExpression));
        Assert.AreEqual(" => true", GraphExpressionDebugFormatter.Format(boolExpression));
    }

    [TestMethod]
    public void Format_throws_for_null_expression() => Assert.ThrowsExactly<ArgumentNullException>(() => GraphExpressionDebugFormatter.Format(null!));

    [TestMethod]
    public void GraphQueryDebugView_formats_filters_traversals_ordering_paging_projection_and_unknown_steps()
    {
        var model = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            SkipCount = 2,
            TakeCount = 3,
            Projection = new GraphScalarProjection
            {
                ResultType = typeof(string),
                SourceKind = GraphProjectionSourceKind.Node,
                SourcePropertyName = nameof(TestPerson.Name)
            }
        };

        model.Orderings.Add(new GraphOrdering
        {
            PropertyName = nameof(TestPerson.Name),
            Descending = true
        });

        model.Steps.Add(new GraphFilterStep
        {
            InputElementType = typeof(TestPerson),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(TestPerson),
            OutputElementKind = GraphQueryElementKind.Node,
            Predicate = (Expression<Func<TestPerson, bool>>)(x => x.Age > 18)
        });

        model.Steps.Add(new GraphTraversalStep
        {
            InputElementType = typeof(TestPerson),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(TestProject),
            OutputElementKind = GraphQueryElementKind.Node,
            Direction = GraphTraversalDirection.Outgoing,
            EdgeType = typeof(TestWorksOn),
            EdgePredicate = (Expression<Func<TestWorksOn, bool>>)(x => x.Role == "Lead"),
            Safety = GraphTraversalSafeties.PreventImmediateCycles
        });

        model.Steps.Add(new GraphEdgeNodeTraversalStep
        {
            InputElementType = typeof(TestWorksOn),
            InputElementKind = GraphQueryElementKind.Edge,
            OutputElementType = typeof(TestProject),
            OutputElementKind = GraphQueryElementKind.Node,
            Endpoint = GraphEdgeEndpoint.To
        });

        model.Steps.Add(new CustomStep
        {
            InputElementType = typeof(TestProject),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(TestProject),
            OutputElementKind = GraphQueryElementKind.Node
        });

        var result = GraphQueryDebugView.Format(model);

        Assert.Contains("GraphQueryModel", result);
        Assert.Contains("Root: TestPerson [Node]", result);
        Assert.Contains("Current: TestProject [Node]", result);
        Assert.Contains("Orderings:", result);
        Assert.Contains("Name DESC", result);
        Assert.Contains("Paging: Skip=2, Take=3", result);
        Assert.Contains("Projection: GraphScalarProjection", result);
        Assert.Contains("FILTER [TestPerson] x => (x.Age > 18)", result);
        Assert.Contains("OUTGOING TestPerson -[TestWorksOn]-> TestProject WHERE EDGE x => (x.Role == \"Lead\")", result);
        Assert.Contains("EDGE-TO TestWorksOn -> TestProject", result);
        Assert.Contains("CustomStep", result);
    }

    [TestMethod]
    public void GraphQueryDebugView_throws_for_null_model() => Assert.ThrowsExactly<ArgumentNullException>(() => GraphQueryDebugView.Format(null!));

    [TestMethod]
    public void Relationship_mapping_to_string_uses_directional_arrow()
    {
        var outgoing = new GraphRelationshipMapping
        {
            Name = "Projects",
            OwnerNodeType = typeof(TestPerson),
            RelatedNodeType = typeof(TestProject),
            EdgeType = typeof(TestWorksOn),
            Direction = GraphTraversalDirection.Outgoing
        };

        var incoming = new GraphRelationshipMapping
        {
            Name = "Workers",
            OwnerNodeType = typeof(TestProject),
            RelatedNodeType = typeof(TestPerson),
            EdgeType = typeof(TestWorksOn),
            Direction = GraphTraversalDirection.Incoming
        };

        Assert.AreEqual("TestPerson --TestWorksOn--> TestProject (Projects)", outgoing.ToString());
        Assert.AreEqual("TestProject <--TestWorksOn-- TestPerson (Workers)", incoming.ToString());
    }

    [TestMethod]
    public void Query_model_current_properties_fall_back_to_root_and_use_last_step_after_traversal()
    {
        var model = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node
        };

        Assert.AreEqual(typeof(TestPerson), model.CurrentElementType);
        Assert.AreEqual(GraphQueryElementKind.Node, model.CurrentElementKind);

        model.Steps.Add(new GraphTraversalStep
        {
            InputElementType = typeof(TestPerson),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(TestProject),
            OutputElementKind = GraphQueryElementKind.Node,
            Direction = GraphTraversalDirection.Outgoing,
            EdgeType = typeof(TestWorksOn)
        });

        Assert.AreEqual(typeof(TestProject), model.CurrentElementType);
        Assert.AreEqual(GraphQueryElementKind.Node, model.CurrentElementKind);
    }

    private sealed class CustomStep : GraphQueryStep
    {
    }
}