using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Permissions;

namespace Polhem.UI.Core.UnitTests.Permissions
{
    /// <summary>
    /// Pure-function decision tests for <see cref="ElementCapabilityResolver"/>: command Can (any-of, no bound model,
    /// null snapshot), the two-step Read/Update downgrade of sensitive fields, and the intersection of grid actions.
    /// </summary>
    public class ElementCapabilityResolverTests
    {
        private static readonly ElementCapabilityResolver s_resolver = ElementCapabilityResolver.Default;

        // PO001 maps to PurchaseOrder. The master table has one ordinary field and one sensitive cost field (`SensitiveCategory=Cost`).
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("PO001", "採購單") { PermissionModelId = "PurchaseOrder" };
            var table = schema.Tables!.Add("PO001", "採購單");
            table.Fields!.Add("sys_name", "單號", FieldDbType.String);
            table.Fields!.Add("total_cost", "成本", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;
            return schema;
        }

        private static Dictionary<string, PermissionAction> Caps(params (string model, PermissionAction action)[] entries)
        {
            var map = new Dictionary<string, PermissionAction>(StringComparer.Ordinal);
            foreach (var (model, action) in entries) { map[model] = action; }
            return map;
        }

        [Fact]
        [DisplayName("Can allows an action that is granted")]
        public void Can_GrantedAction_ReturnsTrue()
        {
            var caps = Caps(("PurchaseOrder", PermissionAction.Create | PermissionAction.Read));

            Assert.True(s_resolver.Can(BuildSchema(), PermissionAction.Create, caps));
        }

        [Fact]
        [DisplayName("Can denies an action that is not granted")]
        public void Can_UngrantedAction_ReturnsFalse()
        {
            var caps = Caps(("PurchaseOrder", PermissionAction.Create | PermissionAction.Read));

            Assert.False(s_resolver.Can(BuildSchema(), PermissionAction.Delete, caps));
        }

        [Fact]
        [DisplayName("Can treats combined flags as any-of: Save=Create|Update is allowed when either is granted")]
        public void Can_CombinedFlags_AnyOf()
        {
            var caps = Caps(("PurchaseOrder", PermissionAction.Update)); // Only Update.

            Assert.True(s_resolver.Can(BuildSchema(), PermissionAction.Create | PermissionAction.Update, caps));
        }

        [Fact]
        [DisplayName("Can always allows an action of None (no bound command)")]
        public void Can_NoneAction_ReturnsTrue()
        {
            var caps = Caps(("PurchaseOrder", PermissionAction.None));

            Assert.True(s_resolver.Can(BuildSchema(), PermissionAction.None, caps));
        }

        [Fact]
        [DisplayName("Can always allows a form that declares no PermissionModelId")]
        public void Can_NoPermissionModel_ReturnsTrue()
        {
            var schema = new FormSchema("PO001", "採購單"); // No PermissionModelId.
            var caps = Caps(("PurchaseOrder", PermissionAction.None));

            Assert.True(s_resolver.Can(schema, PermissionAction.Delete, caps));
        }

        [Fact]
        [DisplayName("Can always allows when the snapshot is null (enforcement is off)")]
        public void Can_NullSnapshot_ReturnsTrue()
        {
            Assert.True(s_resolver.Can(BuildSchema(), PermissionAction.Delete, capabilities: null));
        }

        [Fact]
        [DisplayName("ResolveField does not restrict a non-sensitive field (None)")]
        public void ResolveField_NonSensitive_Allowed()
        {
            var caps = Caps(("Cost", PermissionAction.None));

            var cap = s_resolver.ResolveField(BuildSchema(), "sys_name", tableName: "", caps);

            Assert.Equal(FieldCapability.Allowed, cap);
        }

        [Fact]
        [DisplayName("ResolveField hides a sensitive field without Read")]
        public void ResolveField_SensitiveNoRead_Hidden()
        {
            var caps = Caps(("Cost", PermissionAction.None)); // No permission at all on Cost.

            var cap = s_resolver.ResolveField(BuildSchema(), "total_cost", tableName: "", caps);

            Assert.False(cap.Visible);
        }

        [Fact]
        [DisplayName("ResolveField makes a sensitive field read-only with Read but no Update")]
        public void ResolveField_SensitiveReadNoUpdate_ReadOnly()
        {
            var caps = Caps(("Cost", PermissionAction.Read));

            var cap = s_resolver.ResolveField(BuildSchema(), "total_cost", tableName: "", caps);

            Assert.True(cap.Visible);
            Assert.True(cap.ReadOnly);
        }

        [Fact]
        [DisplayName("ResolveField does not downgrade a sensitive field with Read and Update")]
        public void ResolveField_SensitiveReadUpdate_Allowed()
        {
            var caps = Caps(("Cost", PermissionAction.Read | PermissionAction.Update));

            var cap = s_resolver.ResolveField(BuildSchema(), "total_cost", tableName: "", caps);

            Assert.Equal(FieldCapability.Allowed, cap);
        }

        [Fact]
        [DisplayName("ResolveField does not restrict a sensitive field when the snapshot is null")]
        public void ResolveField_NullSnapshot_Allowed()
        {
            var cap = s_resolver.ResolveField(BuildSchema(), "total_cost", tableName: "", capabilities: null);

            Assert.Equal(FieldCapability.Allowed, cap);
        }
    }
}
