using System.Collections;
using System.Data.Common;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Application.Tracking;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphPerformanceHotPathTests
{
    [TestMethod]
    public void TryGetTrackedEntityByKey_AttachedEntity_ReturnsSameReference()
    {
        var context = new TestGraphContext();
        var person = new TestPerson
        {
            Id = Guid.NewGuid(),
            Name = "Tracked",
            Age = 42
        };

        context.Attach(person);

        var result = context.ChangeTracker.TryGetTrackedEntityByKey(context.Model, typeof(TestPerson), person.Id);

        Assert.AreSame(person, result);
    }

    [TestMethod]
    public void TryGetTrackedEntityByKey_RemovedEntity_ReturnsNull()
    {
        var context = new TestGraphContext();
        var person = new TestPerson
        {
            Id = Guid.NewGuid(),
            Name = "Deleted",
            Age = 42
        };

        context.Attach(person);
        context.Remove(person);

        var result = context.ChangeTracker.TryGetTrackedEntityByKey(context.Model, typeof(TestPerson), person.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task MaterializeListAsync_TrackedDuplicateRows_ReusesSingleAttachedInstance()
    {
        var context = new TestGraphContext();
        var id = Guid.NewGuid();

        var reader = new MaterializerReader(["Id", "Name", "Age"], [[id, "First", 10], [id, "Second", 20]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            TrackingMode = GraphQueryTrackingMode.TrackAll
        };

        var result = await GraphMaterializer.MaterializeListAsync<TestPerson>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(2, result);
        Assert.AreSame(result[0], result[1]);
        Assert.AreEqual("First", result[0].Name);
        Assert.AreEqual(10, result[0].Age);
    }

    [TestMethod]
    public void AddEdgeConnection_SameEdgeInstance_Throws()
    {
        var tracker = new GraphChangeTracker();
        var from = new TestPerson { Id = Guid.NewGuid() };
        var to = new TestProject { Id = Guid.NewGuid() };
        var edge = new TestWorksOn { Id = Guid.NewGuid() };

        tracker.AddEdgeConnection(new PendingEdgeConnection
        {
            Edge = edge,
            FromNode = from,
            ToNode = to
        });

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            tracker.AddEdgeConnection(new PendingEdgeConnection
            {
                Edge = edge,
                FromNode = from,
                ToNode = to
            }));
    }

    [TestMethod]
    public void AddEdgeDisconnection_CancelsPendingConnection()
    {
        var tracker = new GraphChangeTracker();
        var from = new TestPerson { Id = Guid.NewGuid() };
        var to = new TestProject { Id = Guid.NewGuid() };
        var edge = new TestWorksOn { Id = Guid.NewGuid() };

        tracker.Add(edge);

        tracker.AddEdgeConnection(new PendingEdgeConnection
        {
            Edge = edge,
            FromNode = from,
            ToNode = to
        });

        tracker.AddEdgeDisconnection(new PendingEdgeDisconnection
        {
            EdgeType = typeof(TestWorksOn),
            FromNode = from,
            ToNode = to
        });

        Assert.IsEmpty(tracker.PendingEdgeConnections);
        Assert.IsEmpty(tracker.PendingEdgeDisconnections);
        Assert.AreEqual(EntityState.Detached, tracker.Entry(edge).State);
    }

    private sealed class MaterializerReader : DbDataReader
    {
        private readonly string[] _columns;
        private readonly object?[][] _rows;
        private int _index = -1;

        public MaterializerReader(string[] columns, object?[][] rows)
        {
            _columns = columns;
            _rows = rows;
        }

        public override int FieldCount => _columns.Length;
        public override bool HasRows => _rows.Length > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            if (_index + 1 >= _rows.Length)
            {
                return false;
            }

            _index++;
            return true;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override bool NextResult() => false;
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public override string GetName(int ordinal) => _columns[ordinal];
        public override int GetOrdinal(string name) => Array.FindIndex(_columns, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        public override object GetValue(int ordinal) => _rows[_index][ordinal]!;
        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _columns.Length);

            for (var i = 0; i < count; i++)
            {
                values[i] = GetValue(i);
            }

            return count;
        }

        public override bool IsDBNull(int ordinal)
        {
            var value = GetValue(ordinal);
            return value is null || value == DBNull.Value;
        }

        public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => Task.FromResult(IsDBNull(ordinal));
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override string GetDataTypeName(int ordinal) => "object";

        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
        public override IEnumerator GetEnumerator() => _rows.GetEnumerator();
    }

    public TestContext TestContext { get; set; } = null!;
}