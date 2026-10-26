using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;

namespace Polhem.UI.Core.UnitTests.Fields
{
    /// <summary>
    /// Tests the lookup rules of <see cref="LookupFieldRules"/> and <see cref="LookupDisplay"/>.
    /// </summary>
    public class LookupFieldRulesTests
    {
        private static FormField BuildLookupField()
        {
            var field = new FormField("dept_rowid", "Department", FieldDbType.Guid) { RelationProgId = "Dept" };
            field.RelationFieldMappings!.Add("sys_id", "ref_dept_id");
            field.RelationFieldMappings.Add("sys_name", "ref_dept_name");
            return field;
        }

        private static DataRow BuildSelectedRow(Guid rowId, bool includeName = true)
        {
            var table = new DataTable("Dept");
            table.Columns.Add("sys_rowid", typeof(Guid));
            table.Columns.Add("sys_id", typeof(string));
            if (includeName) table.Columns.Add("sys_name", typeof(string));
            var row = table.NewRow();
            row["sys_rowid"] = rowId;
            row["sys_id"] = "D01";
            if (includeName) row["sys_name"] = "Engineering";
            table.Rows.Add(row);
            return row;
        }

        [Fact]
        [DisplayName("HasLookup is true only for a field with a RelationProgId")]
        public void HasLookup_RelationProgId_DecidesLookup()
        {
            Assert.True(LookupFieldRules.HasLookup(BuildLookupField()));
            Assert.False(LookupFieldRules.HasLookup(new FormField("name", "Name", FieldDbType.String)));
            Assert.False(LookupFieldRules.HasLookup(null));
        }

        [Fact]
        [DisplayName("ResolveDisplayFieldNames prefers the layout's DisplayFields over the schema convention")]
        public void ResolveDisplayFieldNames_LayoutWinsOverSchema()
        {
            var field = BuildLookupField();

            Assert.Equal(["ref_dept_id", "ref_dept_name"], LookupFieldRules.ResolveDisplayFieldNames(null, field));
            Assert.Equal(["ref_dept_name"], LookupFieldRules.ResolveDisplayFieldNames(
                new LayoutField { FieldName = "dept_rowid", DisplayFields = " ref_dept_name " }, field));
        }

        [Fact]
        [DisplayName("ApplySelection writes the row id and every mapped field")]
        public void ApplySelection_WritesRowIdAndMappings()
        {
            var rowId = Guid.NewGuid();
            var written = new Dictionary<string, string?>(StringComparer.Ordinal);

            LookupFieldRules.ApplySelection(BuildLookupField(), BuildSelectedRow(rowId), (name, value) => written[name] = value);

            Assert.Equal(FormValueBinding.ToBindingString(rowId), written["dept_rowid"]);
            Assert.Equal("D01", written["ref_dept_id"]);
            Assert.Equal("Engineering", written["ref_dept_name"]);
        }

        [Fact]
        [DisplayName("ApplySelection and ClearSelection include lookup-only mappings")]
        public void ApplySelection_LookupOnlyMapping_IsWrittenAndCleared()
        {
            var field = BuildLookupField();
            field.RelationFieldMappings!.Add(new FieldMapping("sys_id", "dept_code") { LookupOnly = true });
            var written = new Dictionary<string, string?>(StringComparer.Ordinal);

            LookupFieldRules.ApplySelection(field, BuildSelectedRow(Guid.NewGuid()), (name, value) => written[name] = value);
            Assert.Equal("D01", written["dept_code"]);

            LookupFieldRules.ClearSelection(field, (name, value) => written[name] = value);
            Assert.Null(written["dept_code"]);
        }

        [Fact]
        [DisplayName("ApplySelection rejects a mapped source field missing from the lookup result")]
        public void ApplySelection_MissingSourceField_Throws()
        {
            var row = BuildSelectedRow(Guid.NewGuid(), includeName: false);

            var ex = Assert.Throws<InvalidOperationException>(
                () => LookupFieldRules.ApplySelection(BuildLookupField(), row, (_, _) => { }));
            Assert.Contains("LookupFields of form 'Dept'", ex.Message);
        }

        [Fact]
        [DisplayName("ClearSelection empties the field and every mapped destination")]
        public void ClearSelection_EmptiesFieldAndMappings()
        {
            var written = new Dictionary<string, string?>(StringComparer.Ordinal);

            LookupFieldRules.ClearSelection(BuildLookupField(), (name, value) => written[name] = value);

            Assert.Equal(["dept_rowid", "ref_dept_id", "ref_dept_name"], written.Keys.Order(StringComparer.Ordinal));
            Assert.All(written.Values, Assert.Null);
        }

        [Fact]
        [DisplayName("LookupDisplay joins the non-empty values with the separator")]
        public void Compose_SkipsEmptyValues()
        {
            Assert.Equal("D01 - Engineering", LookupDisplay.Compose(["D01", string.Empty, "Engineering"]));
            Assert.Equal(string.Empty, LookupDisplay.Compose([string.Empty]));
        }
    }
}
