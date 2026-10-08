using Gorm.Application.Querying.Models;
using Gorm.Core.Configuration;
using Gorm.Core.Configuration.Interfaces;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Configuration;

[TestClass]
public sealed class ConfigurationBuilderTests
{
    [TestMethod]
    public void NodeTypeBuilder_ToTable_Sets_TableName_And_Schema()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.ToTable("MyTable", "custom");

        var mapping = builder.BuildMapping();

        Assert.AreEqual("MyTable", mapping.TableName);
        Assert.AreEqual("custom", mapping.Schema);
    }

    [TestMethod]
    public void NodeTypeBuilder_ToTable_Defaults_Schema_To_Dbo()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.ToTable("Person");

        var mapping = builder.BuildMapping();

        Assert.AreEqual("dbo", mapping.Schema);
    }

    [TestMethod]
    public void NodeTypeBuilder_ToTable_Throws_For_Empty_TableName()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        Assert.ThrowsExactly<ArgumentException>(() => builder.ToTable(""));
    }

    [TestMethod]
    public void NodeTypeBuilder_ToTable_Throws_For_Empty_Schema()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        Assert.ThrowsExactly<ArgumentException>(() => builder.ToTable("Person", ""));
    }

    [TestMethod]
    public void NodeTypeBuilder_HasKey_Sets_KeyPropertyName()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.HasKey(x => x.Id);

        var mapping = builder.BuildMapping();

        Assert.AreEqual(nameof(TestPerson.Id), mapping.KeyPropertyName);
    }

    [TestMethod]
    public void NodeTypeBuilder_Property_Returns_Same_Builder_For_Same_Property()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        var pb1 = builder.Property(x => x.Name);
        var pb2 = builder.Property(x => x.Name);

        Assert.AreSame(pb1, pb2);
    }

    [TestMethod]
    public void NodeTypeBuilder_HasIndex_Adds_Index_To_Mapping()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.ToTable("Person");
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name);

        var mapping = builder.BuildMapping();

        Assert.HasCount(1, mapping.Indexes);
    }

    [TestMethod]
    public void NodeTypeBuilder_HasUniqueIndex_Adds_Unique_Index()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.ToTable("Person");
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasUniqueIndex(x => x.Name);

        var mapping = builder.BuildMapping();

        Assert.HasCount(1, mapping.Indexes);
        Assert.IsTrue(mapping.Indexes[0].IsUnique);
    }

    [TestMethod]
    public void NodeTypeBuilder_HasIncomingRelationship_Adds_Relationship()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.HasIncomingRelationship<TestWorksOn, TestProject>("IncomingProjects");

        var mapping = builder.BuildMapping();

        var rel = mapping.Relationships.SingleOrDefault(r => r.Name == "IncomingProjects");

        Assert.IsNotNull(rel);
        Assert.AreEqual(GraphTraversalDirection.Incoming, rel.Direction);
    }

    [TestMethod]
    public void NodeTypeBuilder_HasNavigation_Throws_For_Duplicate()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.HasNavigation(x => x.Projects, "Projects");

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.HasNavigation(x => x.Projects, "Projects"));
    }

    [TestMethod]
    public void NodeTypeBuilder_HasOutgoingRelationship_Throws_For_Duplicate_Name()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.HasOutgoingRelationship<TestWorksOn, TestProject>("Projects");

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.HasOutgoingRelationship<TestWorksOn, TestProject>("Projects"));
    }

    [TestMethod]
    public void NodeTypeBuilder_BuildMapping_Contains_Properties()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Age);

        var mapping = builder.BuildMapping();

        Assert.IsTrue(mapping.Properties.Any(p => p.PropertyName == nameof(TestPerson.Name)));
        Assert.IsTrue(mapping.Properties.Any(p => p.PropertyName == nameof(TestPerson.Age)));
    }

    [TestMethod]
    public void NodeTypeBuilder_HasIndex_Throws_For_No_Properties()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        Assert.ThrowsExactly<ArgumentException>(() => builder.HasIndex());
    }

    [TestMethod]
    public void EdgeTypeBuilder_ToTable_Sets_TableName_And_Schema()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.ToTable("WorksOn", "custom");
        builder.From<TestPerson>();
        builder.To<TestProject>();

        var mapping = builder.BuildMapping();

        Assert.AreEqual("WorksOn", mapping.TableName);
        Assert.AreEqual("custom", mapping.Schema);
    }

    [TestMethod]
    public void EdgeTypeBuilder_ToTable_Throws_For_Empty_Name()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        Assert.ThrowsExactly<ArgumentException>(() => builder.ToTable(""));
    }

    [TestMethod]
    public void EdgeTypeBuilder_ToTable_Throws_For_Empty_Schema()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        Assert.ThrowsExactly<ArgumentException>(() => builder.ToTable("WorksOn", ""));
    }

    [TestMethod]
    public void EdgeTypeBuilder_HasKey_Sets_KeyPropertyName()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.HasKey(x => x.Id);
        builder.From<TestPerson>();
        builder.To<TestProject>();

        var mapping = builder.BuildMapping();

        Assert.AreEqual(nameof(TestWorksOn.Id), mapping.KeyPropertyName);
    }

    [TestMethod]
    public void EdgeTypeBuilder_BuildMapping_Throws_Without_From()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.To<TestProject>();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.BuildMapping());
    }

    [TestMethod]
    public void EdgeTypeBuilder_BuildMapping_Throws_Without_To()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.From<TestPerson>();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.BuildMapping());
    }

    [TestMethod]
    public void EdgeTypeBuilder_Property_Returns_Same_Builder_For_Same_Property()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        var pb1 = builder.Property(x => x.Role);
        var pb2 = builder.Property(x => x.Role);

        Assert.AreSame(pb1, pb2);
    }

    [TestMethod]
    public void EdgeTypeBuilder_HasIndex_Adds_Index()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.From<TestPerson>();
        builder.To<TestProject>();
        builder.Property(x => x.Role).HasMaxLength(100);
        builder.HasIndex(x => x.Role);

        var mapping = builder.BuildMapping();

        Assert.HasCount(1, mapping.Indexes);
    }

    [TestMethod]
    public void EdgeTypeBuilder_HasUniqueIndex_Adds_Unique_Index()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.From<TestPerson>();
        builder.To<TestProject>();
        builder.Property(x => x.Role).HasMaxLength(100);
        builder.HasUniqueIndex(x => x.Role);

        var mapping = builder.BuildMapping();

        Assert.IsTrue(mapping.Indexes[0].IsUnique);
    }

    [TestMethod]
    public void EdgeTypeBuilder_BuildMapping_Sets_From_And_To()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.From<TestPerson>();
        builder.To<TestProject>();

        var mapping = builder.BuildMapping();

        Assert.AreEqual(typeof(TestPerson), mapping.FromNodeType);
        Assert.AreEqual(typeof(TestProject), mapping.ToNodeType);
    }

    [TestMethod]
    public void PropertyBuilder_IsRequired_Sets_Flag()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.Property(x => x.Name).IsRequired();

        var mapping = builder.BuildMapping();
        var prop = mapping.Properties.Single(p => p.PropertyName == nameof(TestPerson.Name));

        Assert.IsTrue(prop.IsRequired);
    }

    [TestMethod]
    public void PropertyBuilder_HasMaxLength_Sets_MaxLength()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.Property(x => x.Name).HasMaxLength(150);

        var mapping = builder.BuildMapping();
        var prop = mapping.Properties.Single(p => p.PropertyName == nameof(TestPerson.Name));

        Assert.AreEqual(150, prop.MaxLength);
    }

    [TestMethod]
    public void PropertyBuilder_HasMaxLength_Throws_For_Non_String_Type()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.Property(x => x.Age).HasMaxLength(10));
    }

    [TestMethod]
    public void PropertyBuilder_HasMaxLength_Throws_For_Zero()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.Property(x => x.Name).HasMaxLength(0));
    }

    [TestMethod]
    public void PropertyBuilder_IsRequired_For_ValueType()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.Property(x => x.Age).IsRequired();

        var mapping = builder.BuildMapping();
        var prop = mapping.Properties.Single(p => p.PropertyName == nameof(TestPerson.Age));

        Assert.IsTrue(prop.IsRequired);
    }

    [TestMethod]
    public void GraphIndexBuilder_HasDatabaseName_Changes_Name()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.ToTable("Person");
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_Custom");

        var mapping = builder.BuildMapping();

        Assert.AreEqual("IX_Custom", mapping.Indexes[0].DatabaseName);
    }

    [TestMethod]
    public void GraphIndexBuilder_HasDatabaseName_Throws_For_Empty()
    {
        var nodeBuilder = new NodeTypeBuilder<TestPerson>();

        nodeBuilder.ToTable("Person");

        var indexBuilder = nodeBuilder.HasIndex(x => x.Name);

        Assert.ThrowsExactly<ArgumentException>(() => indexBuilder.HasDatabaseName(""));
    }

    [TestMethod]
    public void GraphIndexBuilder_IsUnique_Can_Be_Chained()
    {
        var nodeBuilder = new NodeTypeBuilder<TestPerson>();

        nodeBuilder.ToTable("Person");
        nodeBuilder.Property(x => x.Name).HasMaxLength(100);
        nodeBuilder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("IX_Unique_Name");

        var mapping = nodeBuilder.BuildMapping();

        Assert.IsTrue(mapping.Indexes[0].IsUnique);
        Assert.AreEqual("IX_Unique_Name", mapping.Indexes[0].DatabaseName);
    }

    [TestMethod]
    public void NodeTypeBuilder_BuildMapping_Automatically_Maps_Public_Scalar_Properties()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        var mapping = builder.BuildMapping();

        Assert.AreEqual(nameof(TestPerson.Id), mapping.KeyPropertyName);
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(TestPerson.Id)));
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(TestPerson.Name)));
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(TestPerson.Age)));
        Assert.IsFalse(mapping.Properties.Any(x => x.PropertyName == nameof(TestPerson.Projects)));
    }

    [TestMethod]
    public void NodeTypeBuilder_Property_Overrides_Auto_Discovered_Metadata()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        var mapping = builder.BuildMapping();
        var property = mapping.Properties.Single(x => x.PropertyName == nameof(TestPerson.Name));

        Assert.AreEqual(100, property.MaxLength);
        Assert.IsTrue(property.IsRequired);
    }

    [TestMethod]
    public void NodeTypeBuilder_HasIndex_Allows_Auto_Discovered_Property()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        builder.HasIndex(x => x.Age);

        var mapping = builder.BuildMapping();

        Assert.HasCount(1, mapping.Indexes);
        Assert.AreEqual(nameof(TestPerson.Age), mapping.Indexes[0].PropertyNames[0]);
    }

    [TestMethod]
    public void NodeTypeBuilder_Id_Is_Default_Key_When_HasKey_Is_Not_Configured()
    {
        var builder = new NodeTypeBuilder<TestPerson>();

        var mapping = builder.BuildMapping();

        Assert.AreEqual(nameof(TestPerson.Id), mapping.KeyPropertyName);
    }

    [TestMethod]
    public void NodeTypeBuilder_HasKey_Can_Override_Default_Id_Key()
    {
        var builder = new NodeTypeBuilder<CustomKeyNode>();

        builder.HasKey(x => x.Code);

        var mapping = builder.BuildMapping();

        Assert.AreEqual(nameof(CustomKeyNode.Code), mapping.KeyPropertyName);
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(CustomKeyNode.Code)));
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(CustomKeyNode.Id)));
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(CustomKeyNode.Name)));
    }

    [TestMethod]
    public void EdgeTypeBuilder_BuildMapping_Automatically_Maps_Public_Scalar_Properties()
    {
        var builder = new EdgeTypeBuilder<TestWorksOn>();

        builder.From<TestPerson>();
        builder.To<TestProject>();

        var mapping = builder.BuildMapping();

        Assert.AreEqual(nameof(TestWorksOn.Id), mapping.KeyPropertyName);
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(TestWorksOn.Id)));
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(TestWorksOn.Role)));
        Assert.IsFalse(mapping.Properties.Any(x => x.PropertyName == nameof(TestWorksOn.FromId)));
        Assert.IsFalse(mapping.Properties.Any(x => x.PropertyName == nameof(TestWorksOn.ToId)));
    }

    [TestMethod]
    public void GraphModelBuilder_ApplyConfiguration_Applies_Node_Configuration()
    {
        var builder = new GraphModelBuilder();

        ApplyCompleteConfiguration(builder);

        var model = builder.Build();
        var mapping = model.GetNode(typeof(TestPerson));

        Assert.AreEqual("ConfiguredPersons", mapping.TableName);
        Assert.IsTrue(mapping.Properties.Any(x => x.PropertyName == nameof(TestPerson.Name)));
    }

    [TestMethod]
    public void GraphModelBuilder_ApplyConfiguration_Applies_Edge_Configuration()
    {
        var builder = new GraphModelBuilder();

        ApplyCompleteConfiguration(builder);

        var model = builder.Build();
        var mapping = model.GetEdge(typeof(TestWorksOn));

        Assert.AreEqual("ConfiguredWorksOn", mapping.TableName);
        Assert.AreEqual(typeof(TestPerson), mapping.FromNodeType);
        Assert.AreEqual(typeof(TestProject), mapping.ToNodeType);
    }

    [TestMethod]
    public void GraphModelBuilder_ApplyConfigurationsFromAssembly_Applies_Filtered_Configurations()
    {
        var builder = new GraphModelBuilder();

        builder.ApplyConfigurationsFromAssembly(
            typeof(ConfigurationBuilderTests).Assembly,
            type => type.Name is
                nameof(PersonNodeConfiguration) or
                nameof(ProjectNodeConfiguration) or
                nameof(WorksOnEdgeConfiguration));

        var model = builder.Build();

        Assert.AreEqual("ConfiguredPersons", model.GetNode(typeof(TestPerson)).TableName);
        Assert.AreEqual("ConfiguredProjects", model.GetNode(typeof(TestProject)).TableName);
        Assert.AreEqual("ConfiguredWorksOn", model.GetEdge(typeof(TestWorksOn)).TableName);
    }

    [TestMethod]
    public void GraphModelBuilder_ApplyConfigurationsFromAssembly_Throws_For_Duplicate_Node_Configurations()
    {
        var builder = new GraphModelBuilder();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => builder.ApplyConfigurationsFromAssembly(
                typeof(ConfigurationBuilderTests).Assembly,
                type => type.Name is nameof(PersonNodeConfiguration) or nameof(DuplicatePersonNodeConfiguration)));
    }

    [TestMethod]
    public void GraphModelBuilder_ApplyConfiguration_Throws_For_Null_Node_Configuration()
    {
        var builder = new GraphModelBuilder();

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.ApplyConfiguration<TestPerson>(null!));
    }

    [TestMethod]
    public void GraphModelBuilder_ApplyConfiguration_Throws_For_Null_Edge_Configuration()
    {
        var builder = new GraphModelBuilder();

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.ApplyConfiguration<TestWorksOn>(null!));
    }

    private static void ApplyCompleteConfiguration(GraphModelBuilder builder)
    {
        builder.ApplyConfiguration(new PersonNodeConfiguration());
        builder.ApplyConfiguration(new ProjectNodeConfiguration());
        builder.ApplyConfiguration(new WorksOnEdgeConfiguration());
    }

    private sealed class PersonNodeConfiguration : IGraphNodeTypeConfiguration<TestPerson>
    {
        public void Configure(NodeTypeBuilder<TestPerson> builder)
        {
            builder.ToTable("ConfiguredPersons");

            builder.Property(x => x.Name).HasMaxLength(200);

            builder.HasOutgoingRelationship<TestWorksOn, TestProject>(nameof(TestPerson.Projects), GraphRelationshipMultiplicity.Many);

            builder.HasNavigation(x => x.Projects, nameof(TestPerson.Projects));
        }
    }

    private sealed class ProjectNodeConfiguration : IGraphNodeTypeConfiguration<TestProject>
    {
        public void Configure(NodeTypeBuilder<TestProject> builder)
        {
            builder.ToTable("ConfiguredProjects");

            builder.Property(x => x.Title).HasMaxLength(200);
        }
    }

    private sealed class WorksOnEdgeConfiguration : IGraphEdgeTypeConfiguration<TestWorksOn>
    {
        public void Configure(EdgeTypeBuilder<TestWorksOn> builder)
        {
            builder.ToTable("ConfiguredWorksOn");
            builder.From<TestPerson>();
            builder.To<TestProject>();

            builder.Property(x => x.Role).HasMaxLength(100);
        }
    }

    private sealed class DuplicatePersonNodeConfiguration : IGraphNodeTypeConfiguration<TestPerson>
    {
        public void Configure(NodeTypeBuilder<TestPerson> builder) =>
            builder.ToTable("DuplicatePersons");
    }

    private sealed class CustomKeyNode : Node
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}