using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Execution;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class UpdateWithoutConcurrencyCommandTests
{
    [TestMethod]
    public void Properties_Can_Be_Set_And_Read()
    {
        var connection = new FakeDbConnection();
        var transaction = connection.BeginTransaction();
        var property = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Name))!;
        var entity = new SampleEntity { Id = Guid.NewGuid(), Name = "Alice" };

        var command = new UpdateWithoutConcurrencyCommand
        {
            Connection = connection,
            Transaction = transaction,
            Schema = "dbo",
            TableName = "Person",
            KeyPropertyName = "Id",
            KeyValue = entity.Id,
            UpdatableProperties = [property],
            Entity = entity
        };

        Assert.AreSame(connection, command.Connection);
        Assert.AreSame(transaction, command.Transaction);
        Assert.AreEqual("dbo", command.Schema);
        Assert.AreEqual("Person", command.TableName);
        Assert.AreEqual("Id", command.KeyPropertyName);
        Assert.AreEqual(entity.Id, command.KeyValue);
        Assert.HasCount(1, command.UpdatableProperties);
        Assert.AreSame(entity, command.Entity);
    }

    private sealed class SampleEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class FakeDbConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = "Fake";
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Open;
        public override void ChangeDatabase(string databaseName)
        { }
        public override void Close()
        { }
        public override void Open()
        { }
        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => new FakeDbTransaction(this);
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        public FakeDbTransaction(DbConnection connection) => _connection = connection;
        public override System.Data.IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit()
        { }
        public override void Rollback()
        { }
    }
}