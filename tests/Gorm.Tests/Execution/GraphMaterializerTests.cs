using System.Collections;
using System.Data.Common;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphMaterializerTests
{
    [TestMethod]
    public async Task MaterializeListAsync_Throws_For_Null_Context()
    {
        var reader = new MaterializerReader([], []);
        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node
        };

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => GraphMaterializer.MaterializeListAsync<TestPerson>(null!, reader, queryModel, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MaterializeListAsync_Throws_For_Unsupported_Projection()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader([], []);
        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = new UnknownProjection { ResultType = typeof(int) }
        };

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => GraphMaterializer.MaterializeListAsync<int>(context, reader, queryModel, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MaterializeListAsync_ScalarProjection_With_NoRows_Returns_Empty_List()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["Age"], []);
        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = new GraphScalarProjection
            {
                ResultType = typeof(int),
                SourcePropertyName = "Age",
                SourceKind = GraphProjectionSourceKind.Node
            }
        };

        var result = await GraphMaterializer.MaterializeListAsync<int>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task MaterializeListAsync_TrackedEntity_Throws_When_Key_Column_Missing()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["Name"], []);
        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            TrackingMode = GraphQueryTrackingMode.TrackAll
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => GraphMaterializer.MaterializeListAsync<TestPerson>(context, reader, queryModel, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MaterializeListAsync_PlainObject_Maps_Values()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["Name", "Age"], [["Alice", 42]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(PersonDto),
            RootElementKind = GraphQueryElementKind.Node,
            TrackingMode = GraphQueryTrackingMode.TrackAll
        };

        var result = await GraphMaterializer.MaterializeListAsync<PersonDto>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Alice", result[0].Name);
        Assert.AreEqual(42, result[0].Age);
    }

    [TestMethod]
    public async Task MaterializeListAsync_TrackedEntity_Reuses_Already_Tracked_Instance()
    {
        var context = new TestGraphContext();
        var existing = new TestPerson { Id = Guid.NewGuid(), Name = "Tracked", Age = 10 };
        context.Attach(existing);

        var reader = new MaterializerReader(["Id", "Name", "Age"], [[existing.Id, "FromDb", 99]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            TrackingMode = GraphQueryTrackingMode.TrackAll
        };

        var result = await GraphMaterializer.MaterializeListAsync<TestPerson>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreSame(existing, result[0]);
        Assert.AreEqual("Tracked", result[0].Name);
    }

    [TestMethod]
    public async Task MaterializeListAsync_TrackedEntity_Throws_When_Key_Is_Null()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["Id", "Name"], [[DBNull.Value, "Alice"]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            TrackingMode = GraphQueryTrackingMode.TrackAll
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => GraphMaterializer.MaterializeListAsync<TestPerson>(context, reader, queryModel, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MaterializeListAsync_NoTracking_Does_Not_Attach_Entities()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["Id", "Name", "Age"], [[Guid.NewGuid(), "Alice", 42]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            TrackingMode = GraphQueryTrackingMode.NoTracking
        };

        var result = await GraphMaterializer.MaterializeListAsync<TestPerson>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.IsEmpty(context.Entries);
    }

    [TestMethod]
    public async Task MaterializeListAsync_ObjectProjection_WholeEntity_Maps_Nested_Object()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["Person__Name", "Person__Age"], [["Alice", 42]]);

        var projection = new GraphObjectProjection
        {
            ResultType = typeof(WrapperDto)
        };

        projection.Bindings.Add(new GraphProjectionBinding
        {
            TargetMemberName = nameof(WrapperDto.Person),
            SourceKind = GraphProjectionSourceKind.Node,
            IsWholeEntity = true
        });

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = projection
        };

        var result = await GraphMaterializer.MaterializeListAsync<WrapperDto>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        var person = result[0].Person;
        Assert.IsNotNull(person);
        Assert.AreEqual("Alice", person.Name);
        Assert.AreEqual(42, person.Age);
    }

    [TestMethod]
    public async Task MaterializeListAsync_ConstructorProjection_Maps_Parameters()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["name", "age"], [["Alice", 42]]);

        var projection = new GraphConstructorProjection
        {
            ResultType = typeof(CtorDto),
            Constructor = typeof(CtorDto).GetConstructor([typeof(string), typeof(int)])!
        };

        projection.Parameters.Add(new GraphConstructorProjectionParameter
        {
            ParameterName = "name",
            ParameterType = typeof(string),
            SourceKind = GraphProjectionSourceKind.Node
        });

        projection.Parameters.Add(new GraphConstructorProjectionParameter
        {
            ParameterName = "age",
            ParameterType = typeof(int),
            SourceKind = GraphProjectionSourceKind.Node
        });

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = projection
        };

        var result = await GraphMaterializer.MaterializeListAsync<CtorDto>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Alice", result[0].Name);
        Assert.AreEqual(42, result[0].Age);
    }

    [TestMethod]
    public async Task MaterializeListAsync_ConstructorProjection_Throws_When_Column_Is_Missing()
    {
        var context = new TestGraphContext();
        var reader = new MaterializerReader(["name"], [["Alice"]]);

        var projection = new GraphConstructorProjection
        {
            ResultType = typeof(CtorDto),
            Constructor = typeof(CtorDto).GetConstructor([typeof(string), typeof(int)])!
        };

        projection.Parameters.Add(new GraphConstructorProjectionParameter
        {
            ParameterName = "name",
            ParameterType = typeof(string),
            SourceKind = GraphProjectionSourceKind.Node
        });

        projection.Parameters.Add(new GraphConstructorProjectionParameter
        {
            ParameterName = "age",
            ParameterType = typeof(int),
            SourceKind = GraphProjectionSourceKind.Node
        });

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node,
            Projection = projection
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => GraphMaterializer.MaterializeListAsync<CtorDto>(context, reader, queryModel, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MaterializeListAsync_PlainObject_Converts_Enum_Guid_And_DateTimeOffset()
    {
        var context = new TestGraphContext();
        var id = Guid.NewGuid();
        var timestamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var reader = new MaterializerReader(["Status", "Id", "OccurredAt"], [["Active", id.ToString(), timestamp]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(ConversionDto),
            RootElementKind = GraphQueryElementKind.Node
        };

        var result = await GraphMaterializer.MaterializeListAsync<ConversionDto>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(ConversionStatus.Active, result[0].Status);
        Assert.AreEqual(id, result[0].Id);
        Assert.AreEqual(new DateTimeOffset(timestamp), result[0].OccurredAt);
    }

    [TestMethod]
    public async Task MaterializeListAsync_PlainObject_Converts_DateOnly_TimeOnly_And_TimeSpan()
    {
        var context = new TestGraphContext();
        var date = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Unspecified);
        var time = new TimeSpan(9, 30, 0);
        var duration = "01:15:00";

        var reader = new MaterializerReader(["Date", "StartTime", "Duration"], [[date, time, duration]]);

        var queryModel = new GraphQueryModel
        {
            RootElementType = typeof(TemporalDto),
            RootElementKind = GraphQueryElementKind.Node
        };

        var result = await GraphMaterializer.MaterializeListAsync<TemporalDto>(context, reader, queryModel, TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(new DateOnly(2026, 7, 8), result[0].Date);
        Assert.AreEqual(new TimeOnly(9, 30), result[0].StartTime);
        Assert.AreEqual(TimeSpan.FromMinutes(75), result[0].Duration);
    }

    private sealed class PersonDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class WrapperDto
    {
        public NestedPersonDto? Person { get; set; }
    }

    private sealed class NestedPersonDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class CtorDto
    {
        public CtorDto(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }
        public int Age { get; }
    }

    private enum ConversionStatus
    {
        Unknown = 0,
        Active = 1
    }

    private sealed class ConversionDto
    {
        public ConversionStatus Status { get; set; }
        public Guid Id { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
    }

    private sealed class TemporalDto
    {
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeSpan Duration { get; set; }
    }

    private sealed class UnknownProjection : GraphQueryProjection
    {
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