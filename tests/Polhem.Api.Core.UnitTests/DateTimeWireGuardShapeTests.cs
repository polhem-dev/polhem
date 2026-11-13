using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// The payload shapes <see cref="DateTimeWireGuard"/> reaches beyond the ones its main tests use: a bare
    /// <see cref="DataSet"/>, the form responses, a request with no filter and a list-valued filter condition.
    /// </summary>
    public class DateTimeWireGuardShapeTests
    {
        private static readonly DateTime s_sample = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        private static DataTable AdoNetShapedTable()
        {
            var table = new DataTable("orders");
            table.Columns.Add(new DataColumn("created_at", typeof(DateTime)));
            return table;
        }

        private static DataSet AdoNetShapedDataSet()
        {
            var dataSet = new DataSet("orders");
            dataSet.Tables.Add(AdoNetShapedTable());
            return dataSet;
        }

        [Fact]
        [DisplayName("The guard throws for a bare DataSet with an UnspecifiedLocal column")]
        public void Validate_BareDataSet_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(AdoNetShapedDataSet()));
        }

        [Fact]
        [DisplayName("The guard throws for the DataSet of a save request, a get-data, a new-data and a save response")]
        public void Validate_FormDataSetMessages_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new SaveRequest { DataSet = AdoNetShapedDataSet() }));
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new GetDataResponse { DataSet = AdoNetShapedDataSet() }));
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new GetNewDataResponse { DataSet = AdoNetShapedDataSet() }));
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new SaveResponse { DataSet = AdoNetShapedDataSet() }));
        }

        [Fact]
        [DisplayName("The guard throws for the table of a list and a lookup response")]
        public void Validate_FormTableMessages_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new GetListResponse { Table = AdoNetShapedTable() }));
            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new GetLookupResponse { Table = AdoNetShapedTable() }));
        }

        [Fact]
        [DisplayName("Messages that carry no DataSet, no table or no filter pass the guard")]
        public void Validate_EmptyMessages_Pass()
        {
            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(new GetListRequest { Filter = null })));
            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(new SaveResponse())));
            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(new GetListResponse())));
        }

        [Fact]
        [DisplayName("A Local DateTime inside a list-valued filter condition is caught")]
        public void Validate_ListValueWithLocalKind_Throws()
        {
            var values = new List<object?> { s_sample, DateTime.SpecifyKind(s_sample, DateTimeKind.Local) };
            var filter = new FilterCondition("created_at", ComparisonOperator.In, values);

            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(new GetListRequest { Filter = filter }));
        }

        [Fact]
        [DisplayName("A list-valued filter condition with only Unspecified values passes the guard")]
        public void Validate_ListValueWithUnspecifiedKind_Passes()
        {
            var filter = new FilterCondition("created_at", ComparisonOperator.In, new List<object?> { s_sample, null });

            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(new GetListRequest { Filter = filter })));
        }
    }
}
