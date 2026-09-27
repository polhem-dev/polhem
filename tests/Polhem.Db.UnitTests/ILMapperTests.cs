using System.ComponentModel;
using System.Data;

namespace Polhem.Db.UnitTests
{
    public class ILMapperTests
    {
        public class SamplePoco
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool Active { get; set; }
        }

        public class NoCtorPoco
        {
            public int Id { get; }

            public NoCtorPoco(int id)
            {
                Id = id;
            }
        }

        private static DataTable BuildTable()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Active", typeof(bool));
            table.Rows.Add(1, "Alice", true);
            table.Rows.Add(2, "Bob", false);
            return table;
        }

        [Fact]
        [DisplayName("CreateMapFunc builds a mapper from the reader's columns")]
        public void CreateMapFunc_MapsAllMatchingFields()
        {
            ILMapper<SamplePoco>.ClearCache();
            using var reader = BuildTable().CreateDataReader();
            reader.Read();

            var mapper = ILMapper<SamplePoco>.CreateMapFunc(reader);
            var result = mapper(reader);

            Assert.Equal(1, result.Id);
            Assert.Equal("Alice", result.Name);
            Assert.True(result.Active);
        }

        [Fact]
        [DisplayName("Column names are matched case-insensitively")]
        public void CreateMapFunc_CaseInsensitiveFieldMatching()
        {
            ILMapper<SamplePoco>.ClearCache();
            var table = new DataTable();
            table.Columns.Add("ID", typeof(int));   // Different casing.
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(99, "Carol");

            using var reader = table.CreateDataReader();
            reader.Read();

            var mapper = ILMapper<SamplePoco>.CreateMapFunc(reader);
            var result = mapper(reader);

            Assert.Equal(99, result.Id);
            Assert.Equal("Carol", result.Name);
        }

        [Fact]
        [DisplayName("A DBNull column keeps the property's default value")]
        public void CreateMapFunc_DBNullField_KeepsDefaultValue()
        {
            ILMapper<SamplePoco>.ClearCache();
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Rows.Add(1, DBNull.Value);

            using var reader = table.CreateDataReader();
            reader.Read();

            var mapper = ILMapper<SamplePoco>.CreateMapFunc(reader);
            var result = mapper(reader);

            Assert.Equal(1, result.Id);
            Assert.Equal(string.Empty, result.Name);  // The property's initial value.
        }

        [Fact]
        [DisplayName("MapToEnumerable maps every row in order")]
        public void MapToEnumerable_MapsAllRows()
        {
            ILMapper<SamplePoco>.ClearCache();
            using var schemaReader = BuildTable().CreateDataReader();
            schemaReader.Read();
            var mapper = ILMapper<SamplePoco>.CreateMapFunc(schemaReader);

            using var reader = BuildTable().CreateDataReader();
            var enumerated = ILMapper<SamplePoco>.MapToEnumerable(reader, mapper).ToList();

            Assert.Equal(2, enumerated.Count);
            Assert.Equal(1, enumerated[0].Id);
            Assert.Equal(2, enumerated[1].Id);
        }

        [Fact]
        [DisplayName("ClearCache clears the cache of the given type")]
        public void ClearCache_RemovesEntriesForType()
        {
            ILMapper<SamplePoco>.ClearCache();
            using var reader = BuildTable().CreateDataReader();
            reader.Read();
            ILMapper<SamplePoco>.CreateMapFunc(reader);

            Assert.True(ILMapper<SamplePoco>.CacheCount > 0);

            ILMapper<SamplePoco>.ClearCache();

            Assert.Equal(0, ILMapper<SamplePoco>.CacheCount);
        }

        [Fact]
        [DisplayName("A type without a parameterless constructor throws InvalidOperationException")]
        public void CreateMapFunc_NoParameterlessCtor_Throws()
        {
            ILMapper<NoCtorPoco>.ClearCache();
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(1);
            using var reader = table.CreateDataReader();
            reader.Read();

            Assert.Throws<InvalidOperationException>(() => ILMapper<NoCtorPoco>.CreateMapFunc(reader));
        }

        [Fact]
        [DisplayName("A second call with the same column layout returns the cached mapper")]
        public void CreateMapFunc_SameShape_ReusesCachedDelegate()
        {
            ILMapper<SamplePoco>.ClearCache();

            using var r1 = BuildTable().CreateDataReader();
            r1.Read();
            var first = ILMapper<SamplePoco>.CreateMapFunc(r1);

            using var r2 = BuildTable().CreateDataReader();
            r2.Read();
            var second = ILMapper<SamplePoco>.CreateMapFunc(r2);

            Assert.Same(first, second);
            Assert.Equal(1, ILMapper<SamplePoco>.CacheCount);
        }

        public class AllTypePoco
        {
            public short S16 { get; set; }
            public int I32 { get; set; }
            public long I64 { get; set; }
            public decimal Dec { get; set; }
            public double D { get; set; }
            public float F { get; set; }
            public DateTime Dt { get; set; }
            public int ReadOnlyProp { get; } = 999; // No setter, so it is skipped.
        }

        [Fact]
        [DisplayName("Each column type maps to the matching GetXXX method of DbDataReader")]
        public void CreateMapFunc_AllSupportedTypes_MapsCorrectly()
        {
            ILMapper<AllTypePoco>.ClearCache();

            var table = new DataTable();
            table.Columns.Add("S16", typeof(short));
            table.Columns.Add("I32", typeof(int));
            table.Columns.Add("I64", typeof(long));
            table.Columns.Add("Dec", typeof(decimal));
            table.Columns.Add("D", typeof(double));
            table.Columns.Add("F", typeof(float));
            table.Columns.Add("Dt", typeof(DateTime));
            // The ReadOnlyProp column exists, but the property has no setter, so it is skipped.
            table.Columns.Add("ReadOnlyProp", typeof(int));

            var dt = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);
            table.Rows.Add((short)12, 34, 56L, 7.89m, 1.23d, 4.56f, dt, 123);

            using var reader = table.CreateDataReader();
            reader.Read();

            var mapper = ILMapper<AllTypePoco>.CreateMapFunc(reader);
            var result = mapper(reader);

            Assert.Equal((short)12, result.S16);
            Assert.Equal(34, result.I32);
            Assert.Equal(56L, result.I64);
            Assert.Equal(7.89m, result.Dec);
            Assert.Equal(1.23d, result.D);
            Assert.Equal(4.56f, result.F);
            Assert.Equal(dt, result.Dt);
            // ReadOnlyProp has no setter, so the mapper does not write
            // it and it keeps the value set in the constructor.
            Assert.Equal(999, result.ReadOnlyProp);
        }

        public class BoolDoublePoco
        {
            public bool Flag { get; set; }
            public double Ratio { get; set; }
        }

        [Fact]
        [DisplayName("bool and double columns map to GetBoolean and GetDouble")]
        public void CreateMapFunc_BoolAndDouble_MapCorrectly()
        {
            ILMapper<BoolDoublePoco>.ClearCache();

            var table = new DataTable();
            table.Columns.Add("Flag", typeof(bool));
            table.Columns.Add("Ratio", typeof(double));
            table.Rows.Add(true, 3.14);

            using var reader = table.CreateDataReader();
            reader.Read();

            var mapper = ILMapper<BoolDoublePoco>.CreateMapFunc(reader);
            var result = mapper(reader);

            Assert.True(result.Flag);
            Assert.Equal(3.14, result.Ratio);
        }

        public class ExtraPropertyPoco
        {
            public int Id { get; set; }
            public string Absent { get; set; } = "default-absent";
        }

        [Fact]
        [DisplayName("A property of T that the reader lacks is skipped and keeps its default value")]
        public void CreateMapFunc_PropertyNotInReader_KeepsDefault()
        {
            ILMapper<ExtraPropertyPoco>.ClearCache();

            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Rows.Add(42);

            using var reader = table.CreateDataReader();
            reader.Read();

            var mapper = ILMapper<ExtraPropertyPoco>.CreateMapFunc(reader);
            var result = mapper(reader);

            Assert.Equal(42, result.Id);
            Assert.Equal("default-absent", result.Absent);
        }
    }
}
