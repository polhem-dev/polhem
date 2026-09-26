using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for JsonRpcException.
    /// </summary>
    public class JsonRpcExceptionTests
    {
        [Fact]
        [DisplayName("The constructor sets HttpStatusCode, ErrorCode, RpcMessage and Message")]
        public void Constructor_SetsAllProperties()
        {
            var ex = new JsonRpcException(400, JsonRpcErrorCode.InvalidRequest, "invalid payload");

            Assert.Equal(400, ex.HttpStatusCode);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, ex.ErrorCode);
            Assert.Equal("invalid payload", ex.RpcMessage);
            Assert.Equal("invalid payload", ex.Message);
        }

        [Theory]
        [InlineData(JsonRpcErrorCode.ParseError)]
        [InlineData(JsonRpcErrorCode.MethodNotFound)]
        [InlineData(JsonRpcErrorCode.Unauthorized)]
        [DisplayName("The constructor keeps the given ErrorCode")]
        public void Constructor_PreservesErrorCode(JsonRpcErrorCode code)
        {
            var ex = new JsonRpcException(500, code, "x");

            Assert.Equal(code, ex.ErrorCode);
        }
    }
}
