using Gorm.Application.Querying.Models;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphConstructorProjectionTests
{
    [TestMethod]
    public void GraphConstructorProjection_Stores_Constructor_And_Parameters()
    {
        var ctor = typeof(PersonProjectionDto).GetConstructor([typeof(string), typeof(int)])!;

        var projection = new GraphConstructorProjection
        {
            ResultType = typeof(PersonProjectionDto),
            Constructor = ctor
        };

        projection.Parameters.Add(new GraphConstructorProjectionParameter
        {
            ParameterName = "name",
            ParameterType = typeof(string),
            SourcePropertyName = "Name",
            SourceKind = GraphProjectionSourceKind.Node,
            IsWholeEntity = false
        });

        projection.Parameters.Add(new GraphConstructorProjectionParameter
        {
            ParameterName = "age",
            ParameterType = typeof(int),
            SourcePropertyName = "Age",
            SourceKind = GraphProjectionSourceKind.Node,
            IsWholeEntity = false
        });

        Assert.AreEqual(typeof(PersonProjectionDto), projection.ResultType);
        Assert.AreSame(ctor, projection.Constructor);
        Assert.HasCount(2, projection.Parameters);
        Assert.AreEqual("name", projection.Parameters[0].ParameterName);
    }

    [TestMethod]
    public void GraphConstructorProjectionParameter_Stores_All_Fields()
    {
        var parameter = new GraphConstructorProjectionParameter
        {
            ParameterName = "person",
            ParameterType = typeof(PersonProjectionDto),
            SourcePropertyName = null,
            SourceKind = GraphProjectionSourceKind.Node,
            IsWholeEntity = true
        };

        Assert.AreEqual("person", parameter.ParameterName);
        Assert.AreEqual(typeof(PersonProjectionDto), parameter.ParameterType);
        Assert.IsNull(parameter.SourcePropertyName);
        Assert.AreEqual(GraphProjectionSourceKind.Node, parameter.SourceKind);
        Assert.IsTrue(parameter.IsWholeEntity);
    }

    private sealed class PersonProjectionDto
    {
        public PersonProjectionDto(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }
        public int Age { get; }
    }
}