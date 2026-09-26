using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Db.Schema;
using Polhem.Db.Schema.Changes;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    public class TableSchemaDiffTests : IClassFixture<SharedDbFixture>
    {
        public TableSchemaDiffTests(SharedDbFixture _) { }

        private static TableSchema BuildSchema(string tableName = "st_demo")
        {
            var schema = new TableSchema { TableName = tableName };
            schema.Fields!.Add("id", "Id", FieldDbType.Guid);
            return schema;
        }

        [Fact]
        [DisplayName("A new Diff has empty Changes and DescriptionChanges")]
        public void Constructor_InitializesEmptyCollections()
        {
            var define = BuildSchema();
            var diff = new TableSchemaDiff(define, realTable: null);

            Assert.Empty(diff.Changes);
            Assert.Empty(diff.DescriptionChanges);
        }

        [Fact]
        [DisplayName("DefineTable and RealTable are exposed")]
        public void Properties_ExposeInputs()
        {
            var define = BuildSchema();
            var real = BuildSchema();
            var diff = new TableSchemaDiff(define, real);

            Assert.Same(define, diff.DefineTable);
            Assert.Same(real, diff.RealTable);
        }

        [Fact]
        [DisplayName("IsNewTable is true when RealTable is null")]
        public void IsNewTable_NullRealTable_ReturnsTrue()
        {
            var diff = new TableSchemaDiff(BuildSchema(), realTable: null);

            Assert.True(diff.IsNewTable);
        }

        [Fact]
        [DisplayName("IsNewTable is false when RealTable is not null")]
        public void IsNewTable_NonNullRealTable_ReturnsFalse()
        {
            var diff = new TableSchemaDiff(BuildSchema(), BuildSchema());

            Assert.False(diff.IsNewTable);
        }

        [Fact]
        [DisplayName("IsEmpty is false when IsNewTable is true")]
        public void IsEmpty_NewTable_ReturnsFalse()
        {
            var diff = new TableSchemaDiff(BuildSchema(), realTable: null);

            Assert.False(diff.IsEmpty);
        }

        [Fact]
        [DisplayName("IsEmpty is true when nothing changed")]
        public void IsEmpty_NoChanges_ReturnsTrue()
        {
            var diff = new TableSchemaDiff(BuildSchema(), BuildSchema());

            Assert.True(diff.IsEmpty);
        }

        [Fact]
        [DisplayName("IsEmpty is false with a structural change")]
        public void IsEmpty_WithStructuralChange_ReturnsFalse()
        {
            var diff = new TableSchemaDiff(BuildSchema(), BuildSchema());
            diff.Changes.Add(new AddFieldChange(new DbField("age", "Age", FieldDbType.Integer)));

            Assert.False(diff.IsEmpty);
        }

        [Fact]
        [DisplayName("IsEmpty is false with a description change")]
        public void IsEmpty_WithDescriptionChange_ReturnsFalse()
        {
            var diff = new TableSchemaDiff(BuildSchema(), BuildSchema());
            diff.DescriptionChanges.Add(new DescriptionChange
            {
                Level = DescriptionLevel.Table,
                NewValue = "示範",
                IsNew = true,
            });

            Assert.False(diff.IsEmpty);
        }
    }
}
