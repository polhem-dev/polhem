using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core.Exceptions;

namespace Polhem.Api.Client.UnitTests.Connectors
{
    /// <summary>
    /// Tests for the error branch of <see cref="ApiConnector"/>,
    /// covering the mapping from <see cref="Polhem.JsonRpc.JsonRpcError.Code"/> back to client-side
    /// exception types (round-trip with <c>PolhemExceptionMapper.MapCode</c>).
    /// </summary>
    /// <remarks>
    /// This file checks the behavior and message shape of individual error codes one by one. Whether the two ends
    /// map consistently, and whether a new code is missed, is guarded by <see cref="ErrorContractDriftTests"/>.
    /// </remarks>
    public class ApiConnectorErrorResponseTests
    {
        [Fact]
        [DisplayName("An error response throws UserMessageException for the UserMessage code with a clean, unprefixed message")]
        public async Task ErrorResponse_UserMessageCode_ThrowsUserMessageException()
        {
            var ex = await Assert.ThrowsAsync<UserMessageException>(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(JsonRpcErrorCode.UserMessage, "欄位不能為空"));

            Assert.Equal("欄位不能為空", ex.Message);
            Assert.DoesNotContain("API error", ex.Message);
        }

        [Fact]
        [DisplayName("An error response throws ForbiddenException for the PermissionDenied code with a clean, unprefixed message")]
        public async Task ErrorResponse_PermissionDeniedCode_ThrowsForbiddenException()
        {
            const string message = "Permission denied: 'Delete' on model 'PurchaseOrder'.";

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(JsonRpcErrorCode.PermissionDenied, message));

            Assert.Equal(message, ex.Message);
            Assert.DoesNotContain("API error", ex.Message);
        }

        [Fact]
        [DisplayName("An error response throws ReplayRejectedException for the ReplayRejected code with a clean, unprefixed message")]
        public async Task ErrorResponse_ReplayRejectedCode_ThrowsReplayRejectedException()
        {
            const string message = "The request timestamp is 90 seconds away from server time, outside the accepted window.";

            var ex = await Assert.ThrowsAsync<ReplayRejectedException>(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(JsonRpcErrorCode.ReplayRejected, message));

            Assert.Equal(message, ex.Message);
            Assert.DoesNotContain("API error", ex.Message);
        }

        [Fact]
        [DisplayName("An error response throws InvalidOperationException for the InternalError code and keeps the prefixed format")]
        public async Task ErrorResponse_InternalErrorCode_ThrowsInvalidOperationException()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(JsonRpcErrorCode.InternalError, "Internal server error"));

            Assert.Contains("API error", ex.Message);
            Assert.Contains("-32000", ex.Message);
            Assert.Contains("Internal server error", ex.Message);
        }

        [Fact]
        [DisplayName("An error response throws InvalidOperationException for other protocol codes such as MethodNotFound (regression)")]
        public async Task ErrorResponse_OtherProtocolCode_ThrowsInvalidOperationException()
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(JsonRpcErrorCode.MethodNotFound, "Method not found"));

            Assert.Contains("-32601", ex.Message);
            Assert.Contains("Method not found", ex.Message);
        }

        [Fact]
        [DisplayName("UserMessageException can be caught by catch (Exception) (regression: existing broad catches still work)")]
        public async Task ErrorResponse_UserMessageException_StillCaughtAsException()
        {
            Exception? caught = null;
            try
            {
                await ApiConnectorTestHost.ExecuteWithErrorAsync(JsonRpcErrorCode.UserMessage, "test");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.IsType<UserMessageException>(caught);
        }

        [Fact]
        [DisplayName("A successful response returns the result (regression)")]
        public async Task SuccessResponse_ReturnsValue()
        {
            var result = await ApiConnectorTestHost.ExecuteWithResultAsync();

            Assert.Equal("ok", result);
        }
    }
}
