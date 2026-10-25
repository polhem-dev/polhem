using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.Controls
{
    /// <summary>
    /// An order with lines, shared by the detail grid and edit form tests.
    /// </summary>
    internal static class OrderTestData
    {
        public static FormSchema BuildSchema()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order").Fields!;
            master.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            master.Add("order_no", "Order No", FieldDbType.String);
            var lines = schema.Tables.Add("OrderLine", "Lines").Fields!;
            lines.Add(new FormField(SysFields.RowId, "Row", FieldDbType.Guid) { Visible = false });
            lines.Add(new FormField(SysFields.MasterRowId, "Order", FieldDbType.Guid) { Visible = false });
            lines.Add(new FormField("item", "Item", FieldDbType.String) { Required = true });
            lines.Add(new FormField("qty", "Qty", FieldDbType.Decimal) { NumberKind = NumberKind.Quantity });
            lines.Add(new FormField("amount", "Amount", FieldDbType.Decimal) { ReadOnly = true, ValueExpression = "qty * 2" });
            return schema;
        }

        public static FormDataObject BuildOrder(FormSchema? schema = null, params string[] items)
        {
            var dataObject = new FormDataObject(schema ?? BuildSchema());
            dataObject.InitializeNewMaster();
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            foreach (var item in items.Length == 0 ? ["Bolt", "Nut"] : items)
            {
                var row = lines.NewRow();
                row["item"] = item;
                lines.Rows.Add(row);
            }
            return dataObject;
        }

        public static LayoutGrid LinesLayout(FormSchema schema, GridControlAllowActions actions = GridControlAllowActions.All)
        {
            var grid = FormLayoutGenerator.Generate(schema, "default").Details!.Single();
            grid.AllowActions = actions;
            return grid;
        }

        public static DataRow Line(FormDataObject dataObject, int index)
            => dataObject.DataSet.Tables["OrderLine"]!.Rows[index];
    }
}
