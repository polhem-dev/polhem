using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Web.Blazor.Server.DataObjects;

namespace Polhem.Web.Blazor.Server.UnitTests.DataObjects
{
    /// <summary>
    /// The row lifecycle of <see cref="FormDataObject"/>: new-row seeding, the row events, and buffered edit
    /// sessions, which the detail grid and its edit form rely on. These mirror the Avalonia data object.
    /// </summary>
    public class FormDataObjectRowLifecycleTests
    {
        private static readonly Guid s_masterRowId = new("bbbbbbbb-0000-0000-0000-000000000001");

        private static FormDataObject BuildDataObject()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order").Fields!;
            master.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            master.Add("order_no", "Order No", FieldDbType.String);
            var detail = schema.Tables.Add("OrderLine", "Line").Fields!;
            detail.Add(SysFields.RowId, "Row", FieldDbType.Guid);
            detail.Add(SysFields.MasterRowId, "Master", FieldDbType.Guid);
            detail.Add("item", "Item", FieldDbType.String);
            detail.Add("qty", "Qty", FieldDbType.Decimal);
            var dataObject = new FormDataObject(schema);
            dataObject.InitializeNewMaster();
            dataObject.MasterRow![SysFields.RowId] = s_masterRowId;
            return dataObject;
        }

        private static DataRow AddLine(FormDataObject dataObject, string item = "Bolt")
        {
            var lines = dataObject.DataSet.Tables["OrderLine"]!;
            var row = lines.NewRow();
            row["item"] = item;
            lines.Rows.Add(row);
            return row;
        }

        [Fact]
        [DisplayName("A new detail row is seeded with a row id, a link to the master and non-null defaults")]
        public void NewRow_IsSeeded()
        {
            var dataObject = BuildDataObject();

            var row = AddLine(dataObject);

            Assert.IsType<Guid>(row[SysFields.RowId]);
            Assert.NotEqual(Guid.Empty, (Guid)row[SysFields.RowId]);
            Assert.Equal(s_masterRowId, row[SysFields.MasterRowId]);
            Assert.Equal(0m, row["qty"]);
        }

        [Fact]
        [DisplayName("Adding and deleting a row raise RowAdded and RowDeleted with the table name")]
        public void AddAndDelete_RaiseRowEvents()
        {
            var dataObject = BuildDataObject();
            var events = new List<string>();
            dataObject.RowAdded += (_, e) => events.Add("added:" + e.TableName);
            dataObject.RowDeleted += (_, e) => events.Add("deleted:" + e.TableName);

            var row = AddLine(dataObject);
            row.Delete();

            Assert.Equal(["added:OrderLine", "deleted:OrderLine"], events);
        }

        [Fact]
        [DisplayName("Writes in an edit session raise RowEditFieldChanged only, and Commit publishes them")]
        public void EditSession_Commit_PublishesChanges()
        {
            var dataObject = BuildDataObject();
            var row = AddLine(dataObject);
            dataObject.InitializeNewMaster();
            row = AddLine(dataObject);
            row.Table.AcceptChanges();
            var proposed = new List<string>();
            var published = new List<string>();
            dataObject.RowEditFieldChanged += (_, e) => proposed.Add(e.FieldName);
            dataObject.FieldValueChanged += (_, e) => published.Add(e.FieldName);

            dataObject.BeginRowEdit(row);
            dataObject.SetField(row, "item", "Nut");
            Assert.Equal("Nut", dataObject.GetField(row, "item"));
            Assert.Empty(published);
            Assert.Contains("item", proposed);

            dataObject.CommitRowEdit(row);

            Assert.Equal(["item"], published);
            Assert.True(dataObject.IsDirty);
        }

        [Fact]
        [DisplayName("Cancelling an edit session restores the row and raises nothing")]
        public void EditSession_Cancel_RestoresSilently()
        {
            var dataObject = BuildDataObject();
            var row = AddLine(dataObject);
            row.Table.AcceptChanges();
            var published = 0;
            dataObject.FieldValueChanged += (_, _) => published++;

            dataObject.BeginRowEdit(row);
            dataObject.SetField(row, "item", "Nut");
            dataObject.CancelRowEdit(row);

            Assert.Equal("Bolt", dataObject.GetField(row, "item"));
            Assert.Equal(0, published);
        }
    }
}
