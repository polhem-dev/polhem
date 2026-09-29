using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Database;

namespace Polhem.Db.UnitTests
{
    public class DbParameterSpecCollectionTests
    {
        [Fact]
        [DisplayName("Add(name, value) infers the DbType and adds the parameter")]
        public void Add_NameValue_AddsAndInfersDbType()
        {
            var collection = new DbParameterSpecCollection();

            var spec = collection.Add("p1", 100);

            Assert.Single(collection);
            Assert.Equal("p1", spec.Name);
            Assert.Equal(100, spec.Value);
            Assert.Equal(DbType.Int32, spec.DbType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [DisplayName("Add(name, value) throws ArgumentException for a blank name")]
        public void Add_NameValue_EmptyName_Throws(string? name)
        {
            var collection = new DbParameterSpecCollection();

            Assert.Throws<ArgumentException>(() => collection.Add(name!, 1));
        }

        [Fact]
        [DisplayName("Add(DbField) builds the parameter from the field definition and sets SourceColumn")]
        public void Add_DbField_BuildsFromField()
        {
            var field = new DbField
            {
                FieldName = "name",
                DbType = FieldDbType.String,
                Length = 50,
                AllowNull = false
            };
            var collection = new DbParameterSpecCollection();

            var spec = collection.Add(field);

            Assert.Equal("name", spec.Name);
            Assert.Equal("name", spec.SourceColumn);
            Assert.Equal(DataRowVersion.Current, spec.SourceVersion);
            Assert.Equal(DbType.String, spec.DbType);
            Assert.Equal(50, spec.Size);
            Assert.NotNull(spec.Value);
        }

        [Fact]
        [DisplayName("Add(DbField) sets Value to null when AllowNull is true")]
        public void Add_DbField_AllowNull_NullValue()
        {
            var field = new DbField
            {
                FieldName = "memo",
                DbType = FieldDbType.String,
                Length = 100,
                AllowNull = true
            };
            var collection = new DbParameterSpecCollection();

            var spec = collection.Add(field);

            Assert.Null(spec.Value);
        }

        [Fact]
        [DisplayName("Add(DbField) sets Size to 0 for a non-String type")]
        public void Add_DbField_NonString_SizeIsZero()
        {
            var field = new DbField
            {
                FieldName = "age",
                DbType = FieldDbType.Integer,
                Length = 4
            };
            var collection = new DbParameterSpecCollection();

            var spec = collection.Add(field);

            Assert.Equal(0, spec.Size);
        }

        [Fact]
        [DisplayName("Add(DbField) accepts a SourceVersion")]
        public void Add_DbField_RespectsSourceVersion()
        {
            var field = new DbField
            {
                FieldName = "id",
                DbType = FieldDbType.Integer
            };
            var collection = new DbParameterSpecCollection();

            var spec = collection.Add(field, DataRowVersion.Original);

            Assert.Equal(DataRowVersion.Original, spec.SourceVersion);
        }
    }
}
