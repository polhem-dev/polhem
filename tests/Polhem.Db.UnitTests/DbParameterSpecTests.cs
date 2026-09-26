using System.ComponentModel;
using System.Data;

namespace Polhem.Db.UnitTests
{
    public class DbParameterSpecTests
    {
        [Fact]
        [DisplayName("The parameterless constructor sets the defaults")]
        public void DefaultConstructor_DefaultValues()
        {
            var spec = new DbParameterSpec();

            Assert.Null(spec.Value);
            Assert.Null(spec.DbType);
            Assert.Null(spec.Size);
            Assert.False(spec.IsNullable);
            Assert.Equal(string.Empty, spec.SourceColumn);
            Assert.Equal(DataRowVersion.Current, spec.SourceVersion);
        }

        [Fact]
        [DisplayName("The value constructor infers the DbType")]
        public void ValueConstructor_InfersDbType()
        {
            var spec = new DbParameterSpec("p1", "hello");

            Assert.Equal("p1", spec.Name);
            Assert.Equal("hello", spec.Value);
            Assert.Equal(DbType.String, spec.DbType);
        }

        [Fact]
        [DisplayName("Name and Key stay in sync")]
        public void Name_SyncsWithKey()
        {
            var spec = new DbParameterSpec();
            spec.Name = "userName";
            Assert.Equal("userName", spec.Key);

            spec.Key = "another";
            Assert.Equal("another", spec.Name);
        }

        [Fact]
        [DisplayName("ToString returns 'Name = Value'")]
        public void ToString_FormatsNameAndValue()
        {
            var spec = new DbParameterSpec("age", 30);

            Assert.Equal("age = 30", spec.ToString());
        }
    }
}
