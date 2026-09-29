using System.ComponentModel;
using System.Reflection;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Repository.Form;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Verifies <c>DataFormRepository.RemoveProtectedFields</c>: even if a deployment's FormSchema declares a
    /// protected field, the field set of the write command must not contain it.
    /// </summary>
    /// <remarks>
    /// This is a defense against privilege escalation, not housekeeping. `st_user` ships no FormSchema, but a
    /// deployment can build its own user maintenance form. Without this removal, that form is a way for an ordinary
    /// user to mark themselves as a deployment admin.
    ///
    /// The private static method is tested directly through reflection, the same approach as the existing
    /// `ConvertDefaultValue` / `TryCoerceToGuid` tests: the defense lives only in this method, and a full Save would
    /// need a physical table while covering the same thing.
    /// </remarks>
    public class DataFormRepositoryProtectedFieldsTests
    {
        private static readonly Type[] s_removeProtectedFieldsParams = [typeof(TableSchema)];

        private static void RemoveProtectedFields(TableSchema tableSchema)
        {
            var method = typeof(DataFormRepository).GetMethod(
                "RemoveProtectedFields", BindingFlags.NonPublic | BindingFlags.Static,
                null, s_removeProtectedFieldsParams, null);
            Assert.NotNull(method);
            method!.Invoke(null, [tableSchema]);
        }

        private static TableSchema BuildUserTableSchema()
        {
            var schema = new FormSchema("st_user", "User");
            var master = schema.Tables!.Add("st_user", "User");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add(SysFields.Id, "User Id", FieldDbType.String);
            master.Fields.Add(ProtectedFields.DeploymentAdmin, "Deployment admin", FieldDbType.Boolean);
            return master.GenerateDbTable();
        }

        [Fact]
        [DisplayName("When a FormSchema declares st_user.deployment_admin, the schema used for writing still excludes it")]
        public void RemoveProtectedFields_DropsDeploymentAdmin()
        {
            var tableSchema = BuildUserTableSchema();
            Assert.True(tableSchema.Fields!.Contains(ProtectedFields.DeploymentAdmin));

            RemoveProtectedFields(tableSchema);

            Assert.False(tableSchema.Fields.Contains(ProtectedFields.DeploymentAdmin));
        }

        [Fact]
        [DisplayName("Removing the protected field leaves the other fields of the table intact")]
        public void RemoveProtectedFields_KeepsOtherColumns()
        {
            var tableSchema = BuildUserTableSchema();

            RemoveProtectedFields(tableSchema);

            Assert.True(tableSchema.Fields!.Contains(SysFields.RowId));
            Assert.True(tableSchema.Fields.Contains(SysFields.Id));
        }

        [Fact]
        [DisplayName("A column with the same name in another table is unaffected (protection matches table and column as a pair)")]
        public void RemoveProtectedFields_OtherTableSameColumn_Kept()
        {
            var schema = new FormSchema("ft_order", "Order");
            var master = schema.Tables!.Add("ft_order", "Order");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add(ProtectedFields.DeploymentAdmin, "Unrelated column", FieldDbType.Boolean);
            var tableSchema = master.GenerateDbTable();

            RemoveProtectedFields(tableSchema);

            Assert.True(tableSchema.Fields!.Contains(ProtectedFields.DeploymentAdmin));
        }
    }
}
