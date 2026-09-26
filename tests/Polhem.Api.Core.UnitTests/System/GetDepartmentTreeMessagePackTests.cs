using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.System;
using Polhem.Definition.Organization;

namespace Polhem.Api.Core.UnitTests.System
{
    /// <summary>
    /// MessagePack round-trip of the GetDepartmentTree wire DTOs: the Tree object on an ApiResponse base (including its
    /// collections) restored across the wire, a null tree, and the empty request edge case.
    /// </summary>
    public class GetDepartmentTreeMessagePackTests
    {
        [Fact]
        [DisplayName("GetDepartmentTreeResponse MessagePack round-trip preserves the Tree and its nodes")]
        public void Response_RoundTrip_PreservesTree()
        {
            var hq = Guid.NewGuid();
            var sales = Guid.NewGuid();
            var response = new GetDepartmentTreeResponse
            {
                Tree = new DepartmentTree("C001",
                [
                    new DepartmentRow(hq, "HQ", "總公司", Guid.Empty, Guid.Empty),
                    new DepartmentRow(sales, "SALES", "業務部", hq, Guid.Empty),
                ]),
            };

            var bytes = MessagePackCodec.Serialize(response);
            var restored = MessagePackCodec.Deserialize<GetDepartmentTreeResponse>(bytes);

            Assert.NotNull(restored.Tree);
            Assert.Equal("C001", restored.Tree!.CompanyId);
            Assert.Single(restored.Tree.Roots!);                              // Nested: a single root (HQ).
            Assert.Equal(2, restored.Tree.GetSelfAndDescendants(hq).Count);
        }

        [Fact]
        [DisplayName("GetDepartmentTreeResponse with a null Tree round-trips without a NullReferenceException")]
        public void Response_NullTree_RoundTrip()
        {
            var bytes = MessagePackCodec.Serialize(new GetDepartmentTreeResponse { Tree = null });
            var restored = MessagePackCodec.Deserialize<GetDepartmentTreeResponse>(bytes);

            Assert.Null(restored.Tree);
        }

        [Fact]
        [DisplayName("GetDepartmentTreeRequest has no wire members yet still goes through serialization and yields a new instance")]
        public void Request_RoundTrip()
        {
            var request = new GetDepartmentTreeRequest();

            var bytes = MessagePackCodec.Serialize(request);
            var restored = MessagePackCodec.Deserialize<GetDepartmentTreeRequest>(bytes);

            // Same reason as in `GetNewDataMessagePackTests`: there are no wire members to compare, so what can be
            // proven is that the formatter is registered and actually ran.
            Assert.NotEmpty(bytes);
            Assert.IsType<GetDepartmentTreeRequest>(restored);
            Assert.NotSame(request, restored);
        }
    }
}
