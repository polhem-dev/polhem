using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Permissions;

namespace Polhem.UI.Core.UnitTests.Permissions
{
    /// <summary>
    /// <see cref="ElementCapabilityResolver"/> when the schema, the field or the snapshot entry is missing.
    /// </summary>
    public class ElementCapabilityResolverEdgeTests
    {
        private static readonly ElementCapabilityResolver s_resolver = ElementCapabilityResolver.Default;

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("PO001", "Purchase order") { PermissionModelId = "PurchaseOrder" };
            var table = schema.Tables!.Add("PO001", "Purchase order");
            table.Fields!.Add("total_cost", "Cost", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;
            return schema;
        }

        private static Dictionary<string, PermissionActions> Caps(string model, PermissionActions actions)
            => new(StringComparer.Ordinal) { [model] = actions };

        [Fact]
        [DisplayName("Can allows an action when there is no schema, since no model binds the form")]
        public void Can_NullSchema_ReturnsTrue()
        {
            Assert.True(s_resolver.Can(null!, PermissionActions.Delete, Caps("PurchaseOrder", PermissionActions.None)));
        }

        [Fact]
        [DisplayName("Can denies an action when the snapshot has no entry for the form's model")]
        public void Can_ModelMissingFromSnapshot_ReturnsFalse()
        {
            Assert.False(s_resolver.Can(BuildSchema(), PermissionActions.Read, Caps("Other", PermissionActions.Read)));
        }

        [Fact]
        [DisplayName("ResolveField does not restrict a field when there is no schema")]
        public void ResolveField_NullSchema_Allowed()
        {
            var cap = s_resolver.ResolveField(null!, "total_cost", "PO001", Caps("Cost", PermissionActions.None));

            Assert.Equal(FieldCapability.Allowed, cap);
        }

        [Fact]
        [DisplayName("ResolveField does not restrict a field the schema does not declare")]
        public void ResolveField_UnknownField_Allowed()
        {
            var cap = s_resolver.ResolveField(BuildSchema(), "missing", "PO001", Caps("Cost", PermissionActions.None));

            Assert.Equal(FieldCapability.Allowed, cap);
        }

        [Fact]
        [DisplayName("ResolveField with no table name looks the field up across the schema")]
        public void ResolveField_NullTableName_FindsField()
        {
            var cap = s_resolver.ResolveField(BuildSchema(), "total_cost", null!, Caps("Cost", PermissionActions.None));

            Assert.False(cap.Visible);
        }

        [Fact]
        [DisplayName("ResolveField hides a sensitive field whose category has no entry in the snapshot")]
        public void ResolveField_CategoryMissingFromSnapshot_Hidden()
        {
            var cap = s_resolver.ResolveField(BuildSchema(), "total_cost", "PO001",
                Caps("PurchaseOrder", PermissionActions.Read | PermissionActions.Update));

            Assert.False(cap.Visible);
            Assert.True(cap.ReadOnly);
        }
    }
}
