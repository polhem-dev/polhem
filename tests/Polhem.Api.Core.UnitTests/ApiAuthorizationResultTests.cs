using System.ComponentModel;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for ApiAuthorizationResult.
    /// </summary>
    public class ApiAuthorizationResultTests
    {
        [Fact]
        [DisplayName("The default constructor gives IsValid=false and AccessToken=Guid.Empty")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var result = new ApiAuthorizationResult();

            Assert.False(result.IsValid);
            Assert.Equal(Guid.Empty, result.AccessToken);
            Assert.Equal(string.Empty, result.ErrorMessage);
        }

        [Fact]
        [DisplayName("Success returns IsValid=true and sets AccessToken")]
        public void Success_SetsValidTrueAndToken()
        {
            var token = Guid.NewGuid();

            var result = ApiAuthorizationResult.Success(token);

            Assert.True(result.IsValid);
            Assert.Equal(token, result.AccessToken);
            Assert.Equal(string.Empty, result.ErrorMessage);
        }

        [Fact]
        [DisplayName("Fail returns IsValid=false and sets Code and ErrorMessage")]
        public void Fail_SetsValidFalseCodeAndMessage()
        {
            var result = ApiAuthorizationResult.Fail(JsonRpcErrorCode.Unauthorized, "無權限");

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.Unauthorized, result.Code);
            Assert.Equal("無權限", result.ErrorMessage);
            Assert.Equal(Guid.Empty, result.AccessToken);
        }

        [Theory]
        [InlineData(JsonRpcErrorCode.InvalidRequest, "invalid")]
        [InlineData(JsonRpcErrorCode.MethodNotFound, "not found")]
        [InlineData(JsonRpcErrorCode.Unauthorized, "unauthorized")]
        [DisplayName("Fail keeps the given Code and ErrorMessage")]
        public void Fail_PreservesCodeAndMessage(JsonRpcErrorCode code, string message)
        {
            var result = ApiAuthorizationResult.Fail(code, message);

            Assert.Equal(code, result.Code);
            Assert.Equal(message, result.ErrorMessage);
        }
    }
}
