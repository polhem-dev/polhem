using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Permissions;

namespace Polhem.UI.Core.UnitTests.Permissions
{
    /// <summary>
    /// <see cref="LayoutCapabilityApplier"/> degrades the sensitive fields of a layout in place, only narrows, and does
    /// nothing without a capability snapshot.
    /// </summary>
    public class LayoutCapabilityApplierTests
    {
        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema("PO001", "PO001") { PermissionModelId = "PurchaseOrder" };
            var table = schema.Tables!.Add("PO001", "PO001");
            table.Fields!.Add("sys_name", "No", FieldDbType.String);
            table.Fields!.Add("total_cost", "Cost", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;
            return schema;
        }

        private static LayoutField Field(FormLayout layout, string name)
            => layout.Sections!.SelectMany(s => s.Fields!).Single(f => f.FieldName == name);

        [Fact]
        [DisplayName("Apply hides a sensitive field whose category the snapshot does not grant Read")]
        public void Apply_CategoryWithoutRead_HidesField()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "PO001");
            var caps = new Dictionary<string, PermissionActions>(StringComparer.Ordinal) { ["PurchaseOrder"] = PermissionActions.Read };

            LayoutCapabilityApplier.Apply(layout, schema, caps);

            Assert.False(Field(layout, "total_cost").Visible);
            Assert.True(Field(layout, "sys_name").Visible);
        }

        [Fact]
        [DisplayName("Apply makes a sensitive field read-only when its category grants Read but not Update")]
        public void Apply_CategoryReadOnly_MarksFieldReadOnly()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "PO001");
            var caps = new Dictionary<string, PermissionActions>(StringComparer.Ordinal)
            {
                [SensitiveCategory.Cost.ToPermissionModelId()] = PermissionActions.Read,
            };

            LayoutCapabilityApplier.Apply(layout, schema, caps);

            Assert.True(Field(layout, "total_cost").Visible);
            Assert.True(Field(layout, "total_cost").ReadOnly);
            Assert.False(Field(layout, "sys_name").ReadOnly);
        }

        [Fact]
        [DisplayName("Apply leaves the layout unchanged when the capability snapshot is null")]
        public void Apply_NullSnapshot_LeavesLayout()
        {
            var schema = BuildSchema();
            var layout = FormLayoutGenerator.Generate(schema, "PO001");

            LayoutCapabilityApplier.Apply(layout, schema, capabilities: null);

            Assert.True(Field(layout, "total_cost").Visible);
            Assert.False(Field(layout, "total_cost").ReadOnly);
        }
    }
}
