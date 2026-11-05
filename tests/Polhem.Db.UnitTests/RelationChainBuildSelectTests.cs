using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Verifies a relation chain three relations deep: a form refers to Employee, whose supervisor comes through
    /// Department and back to Employee (<c>tests/Define/FormSchema/Employee.FormSchema.xml</c>). The form under test is
    /// built in memory, so the shared fixture files stay unchanged. Only the produced SQL is compared.
    /// </summary>
    public class RelationChainBuildSelectTests : IClassFixture<PolhemTestFixture>
    {
        private readonly PolhemTestFixture _fx;

        public RelationChainBuildSelectTests(PolhemTestFixture fx) { _fx = fx; }

        private IDefineAccess DefineAccess => _fx.GetRequiredService<IDefineAccess>();

        private static FormSchema BuildTicketSchema()
        {
            var schema = new FormSchema("Ticket", "Ticket");
            var table = schema.Tables!.Add("Ticket", "Ticket");
            table.DbTableName = "ft_ticket";
            table.Fields!.Add(SysFields.No, "Sequence", FieldDbType.AutoIncrement);
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields!.Add(new FormField("sys_id", "Ticket No", FieldDbType.String) { MaxLength = 20 });
            var owner = table.Fields!.Add("owner_rowid", "Owner", FieldDbType.Guid);
            owner.RelationProgId = "Employee";
            owner.RelationFieldMappings!.Add("sys_name", "ref_owner_name");
            owner.RelationFieldMappings!.Add("ref_supervisor_name", "ref_owner_supervisor_name");
            table.Fields!.Add(new FormField("ref_owner_name", "Owner Name", FieldDbType.String, FieldType.RelationField));
            table.Fields!.Add(new FormField("ref_owner_supervisor_name", "Supervisor Name", FieldDbType.String, FieldType.RelationField));
            return schema;
        }

        private SqlFormCommandBuilder NewBuilder() => new(BuildTicketSchema(), DefineAccess);

        [Fact]
        [DisplayName("BuildSelect selects a field three relations away under the outer form's field name")]
        public void BuildSelect_ThreeRelationsDeep_SelectsUnderOuterFieldName()
        {
            var spec = NewBuilder().BuildSelect("Ticket", "sys_id,ref_owner_supervisor_name", null, null);

            Assert.Contains("AS [ref_owner_supervisor_name]", spec.CommandText);
            Assert.DoesNotContain("AS [ref_supervisor_name]", spec.CommandText);
        }

        [Fact]
        [DisplayName("BuildSelect joins Employee, Department and Employee again for a field three relations away")]
        public void BuildSelect_ThreeRelationsDeep_JoinsEachRelationOnce()
        {
            var spec = NewBuilder().BuildSelect("Ticket", "sys_id,ref_owner_supervisor_name", null, null);

            var lines = spec.CommandText.Split('\n');
            Assert.Equal(3, lines.Count(l => l.Contains("JOIN", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(2, lines.Count(l => l.Contains("JOIN") && l.Contains("st_employee")));
            Assert.Single(lines, l => l.Contains("JOIN") && l.Contains("st_department"));
        }

        [Fact]
        [DisplayName("BuildSelect of every field reads both the near and the three-relation field")]
        public void BuildSelect_AllFields_ReadsNearAndDeepRelationFields()
        {
            var spec = NewBuilder().BuildSelect("Ticket", string.Empty, null, null);

            Assert.Contains("AS [ref_owner_name]", spec.CommandText);
            Assert.Contains("AS [ref_owner_supervisor_name]", spec.CommandText);
        }

        [Fact]
        [DisplayName("BuildSelect filters on a field three relations away")]
        public void BuildSelect_FilterThreeRelationsDeep_ProducesOneParameter()
        {
            var filter = FilterCondition.StartsWith("ref_owner_supervisor_name", "A");

            var spec = NewBuilder().BuildSelect("Ticket", "sys_id", filter, null);

            Assert.Contains("WHERE", spec.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Single(spec.Parameters);
        }
    }
}
