using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.UI.Core;

namespace Avalonia.DemoCenter.Modules.QueryConditions
{
    /// <summary>
    /// The order list the query condition cases share: a schema whose query fields cover every kind of condition, and
    /// rows dated relative to today so the date periods ("this month", "last 7 days") always find some.
    /// </summary>
    internal static class OrderQueryData
    {
        public const string ProgId = "Order";

        /// <summary>Builds the order schema; <see cref="FormSchema.QueryFields"/> names the fields a condition can use.</summary>
        public static FormSchema BuildSchema()
        {
            var schema = new FormSchema(ProgId, "Orders")
            {
                ListFields = "order_id,customer_name,status,order_date,amount,is_paid",
                QueryFields = "order_id,customer_name,status,order_date,amount,is_paid",
            };
            var master = schema.Tables!.Add(ProgId, "Orders");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("order_id", "Order No.", FieldDbType.String);
            master.Fields.Add("customer_name", "Customer", FieldDbType.String);
            var status = master.Fields.Add("status", "Status", FieldDbType.String);
            status.ListItems!.Add("OPEN", "Open");
            status.ListItems.Add("SHIPPED", "Shipped");
            status.ListItems.Add("CANCELLED", "Cancelled");
            master.Fields.Add("order_date", "Order Date", FieldDbType.Date);
            master.Fields.Add("amount", "Amount", FieldDbType.Decimal);
            master.Fields.Add("is_paid", "Paid", FieldDbType.Boolean);
            return schema;
        }

        /// <summary>Builds the order rows, with the columns the views build from <paramref name="schema"/>.</summary>
        public static DataTable SeedRows(FormSchema schema)
        {
            var rows = FormValueBinding.BuildEmptyDataSet(schema).Tables[schema.ProgId]!;
            var today = DateTime.Today;
            AddRow(rows, "SO-1001", "Alfreds Futterkiste", "SHIPPED", today.AddDays(-40), 1250.00m, true);
            AddRow(rows, "SO-1002", "Berglunds snabbköp", "OPEN", today.AddDays(-12), 830.50m, false);
            AddRow(rows, "SO-1003", "Blondel père et fils", "CANCELLED", today.AddDays(-9), 120.00m, false);
            AddRow(rows, "SO-1004", "Bon app'", "SHIPPED", today.AddDays(-6), 2400.00m, true);
            AddRow(rows, "SO-1005", "Bottom-Dollar Markets", "OPEN", today.AddDays(-3), 560.75m, false);
            AddRow(rows, "SO-1006", "Around the Horn", "SHIPPED", today.AddDays(-2), 975.00m, false);
            AddRow(rows, "SO-1007", "Ana Trujillo Emparedados", "OPEN", today.AddDays(-1), 310.20m, true);
            AddRow(rows, "SO-1008", "Antonio Moreno Taquería", "OPEN", today, 1890.00m, false);
            AddRow(rows, "SO-1009", "B's Beverages", "SHIPPED", today.AddDays(-75), 445.00m, true);
            AddRow(rows, "SO-1010", "Cactus Comidas para llevar", "CANCELLED", today.AddDays(-150), 60.00m, false);
            rows.AcceptChanges();
            return rows;
        }

        private static void AddRow(DataTable rows, string orderId, string customer, string status, DateTime ordered,
            decimal amount, bool paid)
        {
            var row = rows.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["order_id"] = orderId;
            row["customer_name"] = customer;
            row["status"] = status;
            row["order_date"] = ordered;
            row["amount"] = amount;
            row["is_paid"] = paid;
            rows.Rows.Add(row);
        }
    }
}
