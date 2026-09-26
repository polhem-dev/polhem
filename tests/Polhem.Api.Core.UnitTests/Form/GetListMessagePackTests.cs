using System.ComponentModel;
using System.Data;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.Form;
using Polhem.Definition.Filters;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Core.UnitTests.Form
{
    /// <summary>
    /// Wire-level round-trip serialization tests of GetListRequest / GetListResponse through
    /// <see cref="MessagePackCodec"/>. The focus: the FilterNode union (FilterGroup + FilterCondition) and the
    /// DataTable formatter restore correctly under the framework's composite resolver.
    /// </summary>
    public class GetListMessagePackTests
    {
        [Fact]
        [DisplayName("GetListRequest with FilterGroup + FilterCondition round-trips intact")]
        public void GetListRequest_RoundTrip_PreservesFilterUnion()
        {
            var rowId = Guid.NewGuid();
            var request = new GetListRequest
            {
                SelectFields = "sys_id,sys_name,ref_dept_name",
                Filter = FilterGroup.All(
                    FilterCondition.Equal("sys_rowid", rowId),
                    FilterGroup.Any(
                        FilterCondition.Equal("ref_dept_id", "D001"),
                        FilterCondition.Equal("ref_dept_id", "D002"))),
                SortFields = [new SortField("ref_dept_name", SortDirection.Asc)],
            };

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetListRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(request.SelectFields, restored!.SelectFields);
            Assert.NotNull(restored.Filter);

            // The outer All group.
            var outerGroup = Assert.IsType<FilterGroup>(restored.Filter);
            Assert.Equal(LogicalOperator.And, outerGroup.Operator);
            Assert.Equal(2, outerGroup.Nodes.Count);

            // First child: the `sys_rowid` condition.
            var firstCondition = Assert.IsType<FilterCondition>(outerGroup.Nodes[0]);
            Assert.Equal("sys_rowid", firstCondition.FieldName);
            Assert.Equal(rowId, firstCondition.Value);

            // Second child: the inner Any group.
            var innerGroup = Assert.IsType<FilterGroup>(outerGroup.Nodes[1]);
            Assert.Equal(LogicalOperator.Or, innerGroup.Operator);
            Assert.Equal(2, innerGroup.Nodes.Count);
            Assert.All(innerGroup.Nodes, n => Assert.IsType<FilterCondition>(n));

            Assert.NotNull(restored.SortFields);
            Assert.Single(restored.SortFields!);
            Assert.Equal("ref_dept_name", restored.SortFields![0].FieldName);
            Assert.Equal(SortDirection.Asc, restored.SortFields[0].Direction);
        }

        [Fact]
        [DisplayName("GetListRequest with every field at its default value round-trips to equal content")]
        public void GetListRequest_DefaultValues_RoundTrip()
        {
            var request = new GetListRequest();

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetListRequest>(bytes);

            Assert.NotNull(restored);
            Assert.Equal(string.Empty, restored!.SelectFields);
            Assert.Null(restored.Filter);
            Assert.Null(restored.SortFields);
            Assert.Null(restored.Paging);
        }

        [Fact]
        [DisplayName("GetListRequest with PagingOptions round-trips Page / PageSize / IncludeTotalCount")]
        public void GetListRequest_RoundTrip_PreservesPagingOptions()
        {
            var request = new GetListRequest
            {
                SelectFields = "sys_id",
                Paging = new PagingOptions { Page = 3, PageSize = 25, IncludeTotalCount = true },
            };

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetListRequest>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Paging);
            Assert.Equal(3, restored.Paging!.Page);
            Assert.Equal(25, restored.Paging.PageSize);
            Assert.True(restored.Paging.IncludeTotalCount);
        }

        [Fact]
        [DisplayName("GetListResponse.Table with a DataTable round-trips its columns and rows")]
        public void GetListResponse_RoundTrip_PreservesDataTable()
        {
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Columns.Add("sys_name", typeof(string));
            table.Columns.Add("ref_dept_name", typeof(string));
            table.Rows.Add("E001", "員工甲", "工程部");
            table.Rows.Add("E002", "員工乙", "業務部");
            var response = new GetListResponse { Table = table };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetListResponse>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Table);
            Assert.Equal(3, restored.Table!.Columns.Count);
            Assert.Equal(2, restored.Table.Rows.Count);
            Assert.Equal("E001", restored.Table.Rows[0]["sys_id"]);
            Assert.Equal("員工甲", restored.Table.Rows[0]["sys_name"]);
            Assert.Equal("工程部", restored.Table.Rows[0]["ref_dept_name"]);
            Assert.Equal("E002", restored.Table.Rows[1]["sys_id"]);
        }

        [Fact]
        [DisplayName("GetListResponse.Table = null round-trips as null")]
        public void GetListResponse_NullTable_RoundTrip()
        {
            var response = new GetListResponse { Table = null };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetListResponse>(bytes);

            Assert.NotNull(restored);
            Assert.Null(restored!.Table);
            Assert.Null(restored.Paging);
        }

        [Fact]
        [DisplayName("GetListResponse with PagingInfo round-trips Page / PageSize / TotalCount / HasMore")]
        public void GetListResponse_RoundTrip_PreservesPagingInfo()
        {
            var response = new GetListResponse
            {
                Paging = new PagingInfo
                {
                    Page = 2,
                    PageSize = 50,
                    TotalCount = 237,
                    HasMore = true,
                },
            };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetListResponse>(bytes);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Paging);
            Assert.Equal(2, restored.Paging!.Page);
            Assert.Equal(50, restored.Paging.PageSize);
            Assert.Equal(237, restored.Paging.TotalCount);
            Assert.True(restored.Paging.HasMore);
        }
    }
}
