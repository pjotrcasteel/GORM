using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Infrastructure.Sql;

namespace Gorm.Tests.Infrastructure;

[TestClass]
public sealed class GraphSqlCommandFactoryDateTimeTests
{
    [TestMethod]
    public void AddParameter_WhenDateOnlyValue_Normalizes_To_DateTime_And_Date_DbType()
    {
        using var command = new CommandFactoryFakeCommand();
        var value = new DateOnly(2026, 7, 8);

        GraphSqlCommandFactory.AddParameter(command, "@date", value);

        var parameter = command.SingleParameter;
        Assert.AreEqual(DbType.Date, parameter.DbType);
        Assert.AreEqual(value.ToDateTime(TimeOnly.MinValue), parameter.Value);
    }

    [TestMethod]
    public void AddParameter_WhenTimeOnlyValue_Normalizes_To_TimeSpan_And_Time_DbType()
    {
        using var command = new CommandFactoryFakeCommand();
        var value = new TimeOnly(9, 30);

        GraphSqlCommandFactory.AddParameter(command, "@time", value);

        var parameter = command.SingleParameter;
        Assert.AreEqual(DbType.Time, parameter.DbType);
        Assert.AreEqual(value.ToTimeSpan(), parameter.Value);
    }

    [TestMethod]
    public void AddParameter_WhenGraphSqlParameterHasDateOnlyValue_Normalizes_To_DateTime_And_Uses_Explicit_Date_DbType()
    {
        using var command = new CommandFactoryFakeCommand();
        var value = new DateOnly(2026, 7, 8);

        GraphSqlCommandFactory.AddParameter(command, GraphSqlParameter.Create("@date", value));

        var parameter = command.SingleParameter;
        Assert.AreEqual(DbType.Date, parameter.DbType);
        Assert.AreEqual(value.ToDateTime(TimeOnly.MinValue), parameter.Value);
    }

    private sealed class CommandFactoryFakeCommand : DbCommand
    {
        private readonly CommandFactoryFakeParameterCollection _parameters = new();

        public DbParameter SingleParameter => (DbParameter)_parameters[0];

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        [AllowNull]
        protected override DbConnection DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new CommandFactoryFakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class CommandFactoryFakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }

        public override void ResetDbType()
        {
        }
    }

    private sealed class CommandFactoryFakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => _items.Any(x => x.ParameterName == value);
        public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(x => x.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);

            if (index >= 0)
            {
                _items.RemoveAt(index);
            }
        }

        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);

            if (index >= 0)
            {
                _items[index] = value;
                return;
            }

            _items.Add(value);
        }
    }
}