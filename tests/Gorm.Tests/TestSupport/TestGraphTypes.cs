using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;

namespace Gorm.Tests.TestSupport;

internal sealed class TestPerson : Node
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public List<TestProject> Projects { get; } = [];
}

internal sealed class TestProject : Node
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class TestWorksOn : Edge
{
    public string Role { get; set; } = string.Empty;
}

internal sealed class TestKnows : Edge
{
    public int Strength { get; set; }
}

internal static class TestGraphModelFactory
{
    public static GraphModel CreateValidModel()
    {
        var personRelationships = new[]
        {
            new GraphRelationshipMapping
            {
                Name = "Projects",
                OwnerNodeType = typeof(TestPerson),
                RelatedNodeType = typeof(TestProject),
                EdgeType = typeof(TestWorksOn),
                Direction = GraphTraversalDirection.Outgoing
            }
        };

        var personNavigations = new[]
        {
            new GraphNavigationMapping
            {
                PropertyName = nameof(TestPerson.Projects),
                RelationshipName = "Projects",
                PropertyType = typeof(List<TestProject>),
                ElementType = typeof(TestProject),
                Kind = GraphNavigationKind.Collection
            }
        };

        return new GraphModel(
            nodes:
            [
                new NodeTypeMapping
                {
                    ClrType = typeof(TestPerson),
                    TableName = "Person",
                    Schema = "dbo",
                    KeyPropertyName = nameof(Node.Id),
                    Properties =
                    [
                        new PropertyMapping
                        {
                            PropertyName = nameof(Node.Id),
                            PropertyType = typeof(Guid),
                            IsRequired = true
                        },
                        new PropertyMapping
                        {
                            PropertyName = nameof(TestPerson.Name),
                            PropertyType = typeof(string),
                            MaxLength = 200
                        }
                    ],
                    Relationships = personRelationships,
                    Navigations = personNavigations,
                    Indexes =
                    [
                        new GraphIndexMapping
                        {
                            DatabaseName = "IX_Person_Name",
                            PropertyNames = [nameof(TestPerson.Name)],
                            IsUnique = false
                        }
                    ]
                },
                new NodeTypeMapping
                {
                    ClrType = typeof(TestProject),
                    TableName = "Project",
                    Schema = "dbo",
                    KeyPropertyName = nameof(Node.Id),
                    Properties =
                    [
                        new PropertyMapping
                        {
                            PropertyName = nameof(Node.Id),
                            PropertyType = typeof(Guid),
                            IsRequired = true
                        },
                        new PropertyMapping
                        {
                            PropertyName = nameof(TestProject.Title),
                            PropertyType = typeof(string),
                            MaxLength = 200
                        }
                    ],
                    Relationships = [],
                    Navigations = [],
                    Indexes = []
                }
            ],
            edges:
            [
                new EdgeTypeMapping
                {
                    ClrType = typeof(TestWorksOn),
                    TableName = "WorksOn",
                    Schema = "dbo",
                    KeyPropertyName = nameof(Edge.Id),
                    FromNodeType = typeof(TestPerson),
                    ToNodeType = typeof(TestProject),
                    Properties =
                    [
                        new PropertyMapping
                        {
                            PropertyName = nameof(Edge.Id),
                            PropertyType = typeof(Guid),
                            IsRequired = true
                        },
                        new PropertyMapping
                        {
                            PropertyName = nameof(TestWorksOn.Role),
                            PropertyType = typeof(string),
                            MaxLength = 100
                        }
                    ],
                    Indexes = []
                }
            ]);
    }

    public static PropertyMapping CreateProperty(Type type, bool isRequired = false, int? maxLength = null, string propertyName = "Value") => new()
    {
        PropertyName = propertyName,
        PropertyType = type,
        IsRequired = isRequired,
        MaxLength = maxLength
    };
}