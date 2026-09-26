using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;
using Polhem.Base;
using Polhem.Base.Exceptions;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// Tests for <see cref="JsonRpcExecutor.MapException"/> covering the mapping from
    /// exception types to (<see cref="JsonRpcErrorCode"/>, message) pairs used in the
    /// JSON-RPC response envelope.
    /// </summary>
    [Collection("SysInfoStatic")]
    public class JsonRpcExecutorUserMessageExceptionTests
    {
        [Fact]
        [DisplayName("MapException returns the UserMessage code and the original message for UserMessageException")]
        public void MapException_UserMessageException_ReturnsUserMessageCode()
        {
            var ex = new UserMessageException("欄位不能為空");

            var (code, message) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
            Assert.Equal("欄位不能為空", message);
        }

        [Fact]
        [DisplayName("MapException returns the PermissionDenied code and the original message for ForbiddenException")]
        public void MapException_ForbiddenException_ReturnsPermissionDeniedCode()
        {
            var ex = new ForbiddenException("Permission denied: 'Delete' on model 'PurchaseOrder'.");

            var (code, message) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.PermissionDenied, code);
            Assert.Equal("Permission denied: 'Delete' on model 'PurchaseOrder'.", message);
        }

        [Fact]
        [DisplayName("MapException returns the CompanyAccessDenied code and the original message for CompanyAccessDeniedException")]
        public void MapException_CompanyAccessDeniedException_ReturnsCompanyAccessDeniedCode()
        {
            // This branch must come before the BCL allowlist. If this type fell through to the allowlist it would be
            // classed as a plain business message (-32099), and the front end could not handle "no right to enter the
            // company" uniformly as a 403.
            var ex = new CompanyAccessDeniedException("Company access denied.");

            var (code, message) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.CompanyAccessDenied, code);
            Assert.Equal("Company access denied.", message);
        }

        [Fact]
        [DisplayName("MapException returns the CompanyNotEntered code and the original message for CompanyNotEnteredException")]
        public void MapException_CompanyNotEnteredException_ReturnsCompanyNotEnteredCode()
        {
            // Not having entered a company is a recoverable protocol state, not a business message for the user.
            // Mapped to -32099, the front end could only pop up the raw message and would have no way to know it
            // should send the user to company selection.
            var ex = new CompanyNotEnteredException("No company has been entered for this session.");

            var (code, message) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.CompanyNotEntered, code);
            Assert.Equal("No company has been entered for this session.", message);
        }

        [Fact]
        [DisplayName("MapException returns the UserMessage code for an allowlisted BCL exception (transitional compatibility)")]
        public void MapException_BclWhitelistException_ReturnsUserMessageCode()
        {
            var ex = new InvalidOperationException("Session state is not valid.");

            var (code, message) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
            Assert.Equal("Session state is not valid.", message);
        }

        [Fact]
        [DisplayName("MapException returns the UserMessage code for ArgumentException")]
        public void MapException_ArgumentException_ReturnsUserMessageCode()
        {
            var ex = new ArgumentException("CompanyId is required.");

            var (code, _) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
        }

        [Fact]
        [DisplayName("MapException returns the UserMessage code for UnauthorizedAccessException")]
        public void MapException_UnauthorizedAccessException_ReturnsUserMessageCode()
        {
            var ex = new UnauthorizedAccessException("Session not found or has expired.");

            var (code, _) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
        }

        [Fact]
        [DisplayName("MapException returns the UserMessage code for FormatException")]
        public void MapException_FormatException_ReturnsUserMessageCode()
        {
            var ex = new FormatException("Invalid method format.");

            var (code, _) = JsonRpcExecutor.MapException(ex);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
        }

        [Fact]
        [DisplayName("MapException returns the InternalError code and a masked message for a non-allowlisted exception (outside debug mode)")]
        public void MapException_UnknownException_ReturnsInternalErrorCode()
        {
            var ex = new MissingMethodException("Method 'DefinitelyNotAMethod' not found.");

            // Masking only applies outside debug mode, and the test process itself runs in debug mode (the
            // SystemSettings in tests/Define). What is verified here is the production behavior.
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = JsonRpcExecutor.MapException(ex);

                Assert.Equal(JsonRpcErrorCode.InternalError, code);
                Assert.Equal("Internal server error", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapException returns the InternalError code for a generic Exception (the original message does not leak outside debug mode)")]
        public void MapException_GenericException_ReturnsInternalErrorCode()
        {
            var ex = new Exception("Some internal failure.");

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = JsonRpcExecutor.MapException(ex);

                Assert.Equal(JsonRpcErrorCode.InternalError, code);
                Assert.Equal("Internal server error", message);
                Assert.DoesNotContain("Some internal failure", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapException passes through the original message of an infrastructure exception in debug mode (the code stays InternalError)")]
        public void MapException_DebugMode_PassesThroughInfrastructureMessage()
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var ex = new MissingMethodException("Method 'DefinitelyNotAMethod' not found.");

                var (code, message) = JsonRpcExecutor.MapException(ex);

                Assert.Equal(JsonRpcErrorCode.InternalError, code);
                Assert.Equal("Method 'DefinitelyNotAMethod' not found.", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapException in debug mode leaves the mapping of user-facing exceptions unchanged")]
        public void MapException_DebugMode_LeavesUserFacingMappingUnchanged()
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var ex = new UserMessageException("欄位不能為空");

                var (code, message) = JsonRpcExecutor.MapException(ex);

                Assert.Equal(JsonRpcErrorCode.UserMessage, code);
                Assert.Equal("欄位不能為空", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }
    }
}
