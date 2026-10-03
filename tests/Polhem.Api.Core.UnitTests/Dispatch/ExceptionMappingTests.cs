using System.ComponentModel;
using Polhem.Api.Core.Dispatch;
using Polhem.Api.Core.JsonRpc;
using Polhem.Core;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Storage;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Tests for <see cref="PolhemExceptionMapper.MapCode"/> covering the mapping from
    /// exception types to (<see cref="JsonRpcErrorCode"/>, message) pairs used in the
    /// JSON-RPC response envelope.
    /// </summary>
    [Collection(SysInfoStaticCollection.Name)]
    public class ExceptionMappingTests
    {
        [Fact]
        [DisplayName("MapCode returns the UserMessage code and the original message for UserMessageException")]
        public void MapCode_UserMessageException_ReturnsUserMessageCode()
        {
            var ex = new UserMessageException("欄位不能為空");

            var (code, message) = PolhemExceptionMapper.MapCode(ex);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
            Assert.Equal("欄位不能為空", message);
        }

        [Fact]
        [DisplayName("MapCode returns the PermissionDenied code and the original message for ForbiddenException")]
        public void MapCode_ForbiddenException_ReturnsPermissionDeniedCode()
        {
            var ex = new ForbiddenException("Permission denied: 'Delete' on model 'PurchaseOrder'.");

            var (code, message) = PolhemExceptionMapper.MapCode(ex);

            Assert.Equal(JsonRpcErrorCode.PermissionDenied, code);
            Assert.Equal("Permission denied: 'Delete' on model 'PurchaseOrder'.", message);
        }

        [Fact]
        [DisplayName("MapCode returns the CompanyAccessDenied code and the original message for CompanyAccessDeniedException")]
        public void MapCode_CompanyAccessDeniedException_ReturnsCompanyAccessDeniedCode()
        {
            // This branch must come before the BCL allowlist. If this type fell through to the allowlist it would be
            // classed as a plain business message (-32099), and the front end could not handle "no right to enter the
            // company" uniformly as a 403.
            var ex = new CompanyAccessDeniedException("Company access denied.");

            var (code, message) = PolhemExceptionMapper.MapCode(ex);

            Assert.Equal(JsonRpcErrorCode.CompanyAccessDenied, code);
            Assert.Equal("Company access denied.", message);
        }

        [Fact]
        [DisplayName("MapCode returns the CompanyNotEntered code and the original message for CompanyNotEnteredException")]
        public void MapCode_CompanyNotEnteredException_ReturnsCompanyNotEnteredCode()
        {
            // Not having entered a company is a recoverable protocol state, not a business message for the user.
            // Mapped to -32099, the front end could only pop up the raw message and would have no way to know it
            // should send the user to company selection.
            var ex = new CompanyNotEnteredException("No company has been entered for this session.");

            var (code, message) = PolhemExceptionMapper.MapCode(ex);

            Assert.Equal(JsonRpcErrorCode.CompanyNotEntered, code);
            Assert.Equal("No company has been entered for this session.", message);
        }

        [Theory]
        [DisplayName("MapCode sends a BCL exception under the UserMessage code with a fixed message outside debug mode")]
        [InlineData(typeof(InvalidOperationException), "The request could not be completed.")]
        [InlineData(typeof(ObjectDisposedException), "The request could not be completed.")]
        [InlineData(typeof(ArgumentException), "The request is not valid.")]
        [InlineData(typeof(ArgumentNullException), "The request is not valid.")]
        [InlineData(typeof(ArgumentOutOfRangeException), "The request is not valid.")]
        [InlineData(typeof(UnauthorizedAccessException), "Access denied.")]
        [InlineData(typeof(NotSupportedException), "The request is not supported.")]
        [InlineData(typeof(FormatException), "The request is not valid.")]
        public void MapCode_BclException_ReturnsFixedMessage(Type exceptionType, string expected)
        {
            // These families carry internal detail from the BCL, the drivers and the framework's own
            // infrastructure — table and parameter names, database identifiers, parser output — so their own
            // text must not reach a remote caller.
            var ex = (Exception)Activator.CreateInstance(exceptionType, "DatabaseServer 'db1' referenced by 'ft_secret'")!;

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = PolhemExceptionMapper.MapCode(ex);

                Assert.Equal(JsonRpcErrorCode.UserMessage, code);
                Assert.Equal(expected, message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapCode passes a BCL exception's own message through in debug mode")]
        public void MapCode_BclException_DebugMode_PassesMessageThrough()
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var (code, message) = PolhemExceptionMapper.MapCode(new InvalidOperationException("Session state is not valid."));

                Assert.Equal(JsonRpcErrorCode.UserMessage, code);
                Assert.Equal("Session state is not valid.", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapCode keeps the message of the framework's own JsonRpcException outside debug mode")]
        public void MapCode_JsonRpcException_KeepsItsMessage()
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = PolhemExceptionMapper.MapCode(
                    new JsonRpcException(400, JsonRpcErrorCode.InvalidRequest, "Missing method"));

                Assert.Equal(JsonRpcErrorCode.UserMessage, code);
                Assert.Equal("Missing method", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapCode sends a missing definition under the UserMessage code with its own message outside debug mode")]
        public void MapCode_DefinitionNotFoundException_KeepsItsMessage()
        {
            var ex = new DefinitionNotFoundException(
                DefineType.FormSchema, "Employee", Path.Combine("srv", "Define", "FormSchema", "Employee.FormSchema.xml"));

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = PolhemExceptionMapper.MapCode(ex);

                Assert.Equal(JsonRpcErrorCode.UserMessage, code);
                Assert.Equal("FormSchema 'Employee' not found.", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("Only framework-owned exception types are declared verbatim in the error contract")]
        public void ErrorContract_VerbatimRows_AreFrameworkTypesOnly()
        {
            var bclAssembly = typeof(object).Assembly;

            foreach (var row in JsonRpcErrorContract.Rows)
            {
                if (row.IsVerbatim)
                    Assert.NotEqual(bclAssembly, row.ExceptionType.Assembly);
                else if (row.Code is not (JsonRpcErrorCode.MethodNotFound or JsonRpcErrorCode.InvalidParams))
                    Assert.Equal(bclAssembly, row.ExceptionType.Assembly);
            }

            // The protocol errors are the one place a framework type travels with a fixed message: their own
            // text names server types for the log, and the code already tells the caller what went wrong.
            Assert.Contains(JsonRpcErrorContract.Rows, row => row.Code == JsonRpcErrorCode.MethodNotFound && !row.IsVerbatim);
            Assert.Contains(JsonRpcErrorContract.Rows, row => row.Code == JsonRpcErrorCode.InvalidParams && !row.IsVerbatim);
        }

        [Fact]
        [DisplayName("MapCode returns the InternalError code and a masked message for a non-allowlisted exception (outside debug mode)")]
        public void MapCode_UnknownException_ReturnsInternalErrorCode()
        {
            var ex = new MissingMethodException("Method 'DefinitelyNotAMethod' not found.");

            // Masking only applies outside debug mode, and the test process itself runs in debug mode (the
            // SystemSettings in tests/Define). What is verified here is the production behavior.
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = PolhemExceptionMapper.MapCode(ex);

                Assert.Equal(JsonRpcErrorCode.InternalError, code);
                Assert.Equal("Internal server error", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapCode returns the InternalError code for a generic Exception (the original message does not leak outside debug mode)")]
        public void MapCode_GenericException_ReturnsInternalErrorCode()
        {
            var ex = new Exception("Some internal failure.");

            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = false;
                var (code, message) = PolhemExceptionMapper.MapCode(ex);

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
        [DisplayName("MapCode passes through the original message of an infrastructure exception in debug mode (the code stays InternalError)")]
        public void MapCode_DebugMode_PassesThroughInfrastructureMessage()
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var ex = new MissingMethodException("Method 'DefinitelyNotAMethod' not found.");

                var (code, message) = PolhemExceptionMapper.MapCode(ex);

                Assert.Equal(JsonRpcErrorCode.InternalError, code);
                Assert.Equal("Method 'DefinitelyNotAMethod' not found.", message);
            }
            finally
            {
                SysInfo.IsDebugMode = original;
            }
        }

        [Fact]
        [DisplayName("MapCode in debug mode leaves the mapping of user-facing exceptions unchanged")]
        public void MapCode_DebugMode_LeavesUserFacingMappingUnchanged()
        {
            bool original = SysInfo.IsDebugMode;
            try
            {
                SysInfo.IsDebugMode = true;
                var ex = new UserMessageException("欄位不能為空");

                var (code, message) = PolhemExceptionMapper.MapCode(ex);

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
