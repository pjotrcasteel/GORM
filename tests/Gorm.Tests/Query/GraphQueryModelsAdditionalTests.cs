using System.Linq.Expressions;
using Gorm.Application.Querying.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryModelsAdditionalTests
{
    [TestMethod]
    public void GraphQueryExecutionPlan_EffectiveRowLimit_Is_Calculated_Per_Operator()
    {
        Assert.AreEqual(1, new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.First }.EffectiveRowLimit);
        Assert.AreEqual(1, new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.FirstOrDefault }.EffectiveRowLimit);
        Assert.AreEqual(2, new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.Single }.EffectiveRowLimit);
        Assert.AreEqual(2, new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.SingleOrDefault }.EffectiveRowLimit);
        Assert.IsNull(new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.ToList }.EffectiveRowLimit);
    }

    [TestMethod]
    public void GraphQueryExecutionPlan_IsExistenceCheck_True_Only_For_Any()
    {
        Assert.IsTrue(new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.Any }.IsExistenceCheck);
        Assert.IsFalse(new GraphQueryExecutionPlan { TerminalOperator = GraphTerminalOperatorKind.First }.IsExistenceCheck);
    }

    [TestMethod]
    public void GraphQueryShape_AddOrdering_And_SetTake_Works()
    {
        var shape = new GraphQueryShape();
        var ordering = new GraphOrdering { PropertyName = "Name", Descending = true, IsProjected = false };

        shape.AddOrdering(ordering);
        shape.SetTake(5);

        Assert.HasCount(1, shape.Orderings);
        Assert.AreEqual("Name", shape.Orderings[0].PropertyName);
        Assert.AreEqual(5, shape.Take);
    }

    [TestMethod]
    public void GraphQueryShape_SetTake_Throws_For_Zero_Or_Negative()
    {
        var shape = new GraphQueryShape();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => shape.SetTake(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => shape.SetTake(-1));
    }

    [TestMethod]
    public void GraphQueryModel_CurrentElement_Uses_Root_Without_Steps_And_LastStep_With_Steps()
    {
        var model = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node
        };

        Assert.AreEqual(typeof(TestPerson), model.CurrentElementType);
        Assert.AreEqual(GraphQueryElementKind.Node, model.CurrentElementKind);

        model.Steps.Add(new GraphFilterStep
        {
            InputElementType = typeof(TestPerson),
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = typeof(TestProject),
            OutputElementKind = GraphQueryElementKind.Node,
            Predicate = (Expression<Func<TestPerson, bool>>)(x => x.Age > 18),
            IsProjected = false
        });

        Assert.AreEqual(typeof(TestProject), model.CurrentElementType);
        Assert.AreEqual(GraphQueryElementKind.Node, model.CurrentElementKind);
    }

    [TestMethod]
    public void GraphQueryModel_Clone_Copies_Metadata_And_Orderings()
    {
        var model = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            IsDistinct = true,
            SkipCount = 1,
            TakeCount = 2,
            TrackingMode = GraphQueryTrackingMode.NoTracking,
            Projection = new GraphScalarProjection
            {
                ResultType = typeof(string),
                SourcePropertyName = "Name",
                SourceKind = GraphProjectionSourceKind.Node
            }
        };

        model.Orderings.Add(new GraphOrdering
        {
            PropertyName = "Name",
            Descending = true,
            IsProjected = false
        });

        var clone = model.Clone();

        Assert.AreEqual(model.RootElementType, clone.RootElementType);
        Assert.AreEqual(model.RootElementKind, clone.RootElementKind);
        Assert.AreEqual(model.IsDistinct, clone.IsDistinct);
        Assert.AreEqual(model.SkipCount, clone.SkipCount);
        Assert.AreEqual(model.TakeCount, clone.TakeCount);
        Assert.AreEqual(model.TrackingMode, clone.TrackingMode);
        Assert.HasCount(1, clone.Orderings);
        Assert.AreEqual("Name", clone.Orderings[0].PropertyName);
        Assert.AreNotSame(model.Orderings[0], clone.Orderings[0]);

        model.Orderings[0] = new GraphOrdering
        {
            PropertyName = "Changed",
            Descending = false,
            IsProjected = false
        };

        Assert.AreEqual("Name", clone.Orderings[0].PropertyName);
    }
}