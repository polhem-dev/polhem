using System.ComponentModel;
using Polhem.Db.Dml;

namespace Polhem.Db.UnitTests.Dml
{
    /// <summary>
    /// Unit tests for <c>QueryFieldMapping</c>.
    /// </summary>
    public class QueryFieldMappingTests
    {
        [Fact]
        [DisplayName("The default constructor initializes empty strings and a null TableJoin")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var mapping = new QueryFieldMapping();

            Assert.Equal(string.Empty, mapping.FieldName);
            Assert.Equal(string.Empty, mapping.Key);
            Assert.Equal(string.Empty, mapping.SourceAlias);
            Assert.Equal(string.Empty, mapping.SourceField);
            Assert.Null(mapping.TableJoin);
        }

        [Fact]
        [DisplayName("FieldName and Key map to each other")]
        public void FieldName_MapsToKey()
        {
            var mapping = new QueryFieldMapping { FieldName = "Alpha" };
            Assert.Equal("Alpha", mapping.Key);

            mapping.Key = "Beta";
            Assert.Equal("Beta", mapping.FieldName);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var join = new TableJoin { Key = "join1" };
            var mapping = new QueryFieldMapping
            {
                FieldName = "UserName",
                SourceAlias = "U",
                SourceField = "name",
                TableJoin = join
            };

            Assert.Equal("UserName", mapping.FieldName);
            Assert.Equal("U", mapping.SourceAlias);
            Assert.Equal("name", mapping.SourceField);
            Assert.Same(join, mapping.TableJoin);
        }

        [Fact]
        [DisplayName("ToString returns \"{SourceAlias}.{SourceField} AS {FieldName}\"")]
        public void ToString_ReturnsFormatted()
        {
            var mapping = new QueryFieldMapping
            {
                FieldName = "UserName",
                SourceAlias = "U",
                SourceField = "name"
            };

            Assert.Equal("U.name AS UserName", mapping.ToString());
        }

        [Fact]
        [DisplayName("ToString returns \". AS \" for the default empty values")]
        public void ToString_DefaultValues_ReturnsEmptyFormatted()
        {
            var mapping = new QueryFieldMapping();

            Assert.Equal(". AS ", mapping.ToString());
        }
    }
}
