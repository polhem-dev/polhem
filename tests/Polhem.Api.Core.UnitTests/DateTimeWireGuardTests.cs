using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.AuditLog;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Verifies the wire invariants of ADR-032 D6: a `DataSet` is checked for `DateTimeMode`, and a loose `DateTime` for `Kind`.
    /// </summary>
    public class DateTimeWireGuardTests
    {
        private static readonly DateTime s_sample = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

        private static DataTable AdoNetShapedTable()
        {
            // What DbDataAdapter.Fill / DataTable.Load leave behind: DateTimeMode is the .NET default.
            var table = new DataTable("orders");
            table.Columns.Add(new DataColumn("created_at", typeof(DateTime)));
            table.Rows.Add(s_sample);
            return table;
        }

        [Fact]
        [DisplayName("The guard throws for a DataTable with an UnspecifiedLocal column")]
        public void Validate_DataTableWithUnspecifiedLocalColumn_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => DateTimeWireGuard.Validate(AdoNetShapedTable()));

            Assert.Contains("created_at", exception.Message, StringComparison.Ordinal);
            Assert.Contains("UnspecifiedLocal", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A normalized DataTable passes the guard")]
        public void Validate_NormalizedDataTable_Passes()
        {
            var table = AdoNetShapedTable();
            table.NormalizeDateTimeMode();

            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(table)));
        }

        [Fact]
        [DisplayName("A DataTable built with AddColumn passes the guard")]
        public void Validate_FrameworkBuiltDataTable_Passes()
        {
            var table = new DataTable("orders");
            table.AddColumn("created_at", FieldDbType.DateTime);
            table.AddColumn("order_date", FieldDbType.Date);

            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(table)));
        }

        [Fact]
        [DisplayName("The guard throws when any table in a DataSet violates the rule")]
        public void Validate_DataSetWithOneOffendingTable_Throws()
        {
            using var dataSet = new DataSet("s");
            var clean = new DataTable("clean");
            clean.AddColumn("created_at", FieldDbType.DateTime);
            dataSet.Tables.Add(clean);
            dataSet.Tables.Add(AdoNetShapedTable());

            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(dataSet));
        }

        [Fact]
        [DisplayName("The guard throws for a GetChangeDetailResponse carrying an offending DataSet")]
        public void Validate_ChangeDetailResponseWithOffendingDataSet_Throws()
        {
            using var dataSet = new DataSet("s");
            dataSet.Tables.Add(AdoNetShapedTable());
            var response = new GetChangeDetailResponse { DataSet = dataSet };

            Assert.Throws<InvalidOperationException>(() => DateTimeWireGuard.Validate(response));
        }

        [Fact]
        [DisplayName("Non-DateTime columns are not subject to the DateTimeMode check")]
        public void Validate_TableWithoutDateTimeColumns_Passes()
        {
            var table = new DataTable("orders");
            table.Columns.Add(new DataColumn("remark", typeof(string)));

            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(table)));
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [DisplayName("A FilterCondition value with Kind Unspecified or Utc passes the guard")]
        public void Validate_FilterConditionWithNonLocalKind_Passes(DateTimeKind kind)
        {
            var filter = FilterCondition.Equal("created_at", DateTime.SpecifyKind(s_sample, kind));

            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(Request(filter))));
        }

        [Fact]
        [DisplayName("The guard throws for a FilterCondition value with Kind=Local")]
        public void Validate_FilterConditionWithLocalKind_Throws()
        {
            var filter = FilterCondition.Equal("created_at", DateTime.SpecifyKind(s_sample, DateTimeKind.Local));

            var exception = Assert.Throws<InvalidOperationException>(
                () => DateTimeWireGuard.Validate(Request(filter)));

            Assert.Contains("created_at", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("An offending value inside a nested FilterGroup is caught as well")]
        public void Validate_NestedFilterGroupWithLocalKind_Throws()
        {
            var nested = FilterGroup.All(
                FilterCondition.Equal("status", "open"),
                FilterGroup.All(
                    FilterCondition.Equal("created_at", DateTime.SpecifyKind(s_sample, DateTimeKind.Local))));

            Assert.Throws<InvalidOperationException>(
                () => DateTimeWireGuard.Validate(Request(nested)));
        }

        [Fact]
        [DisplayName("The SecondValue of a Between condition is checked as well")]
        public void Validate_FilterConditionSecondValueWithLocalKind_Throws()
        {
            var filter = new FilterCondition(
                "created_at",
                ComparisonOperator.Between,
                DateTime.SpecifyKind(s_sample, DateTimeKind.Utc),
                DateTime.SpecifyKind(s_sample, DateTimeKind.Local));

            Assert.Throws<InvalidOperationException>(
                () => DateTimeWireGuard.Validate(Request(filter)));
        }

        [Fact]
        [DisplayName("A DateOnly filter value is not subject to the Kind check")]
        public void Validate_FilterConditionWithDateOnly_Passes()
        {
            var filter = FilterCondition.Equal("order_date", new DateOnly(2026, 1, 1));

            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(Request(filter))));
        }

        [Fact]
        [DisplayName("null and uncovered types always pass")]
        public void Validate_NullOrUnknownValue_Passes()
        {
            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate(null)));
            Assert.Null(Record.Exception(() => DateTimeWireGuard.Validate("plain string")));
        }

        private static GetListRequest Request(FilterNode? filter) => new GetListRequest { Filter = filter };
    }
}
