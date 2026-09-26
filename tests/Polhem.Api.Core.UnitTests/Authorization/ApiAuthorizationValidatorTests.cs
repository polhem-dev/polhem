using System.ComponentModel;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Core.UnitTests.Authorization
{
    /// <summary>
    /// ApiAuthorizationValidator unit tests.
    /// </summary>
    public class ApiAuthorizationValidatorTests
    {
        private static ApiAuthorizationValidator CreateValidator() => new();

        [Fact]
        [DisplayName("Validate fails with InvalidRequest for a null context")]
        public void Validate_NullContext_Fails()
        {
            var result = CreateValidator().Validate(null!);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Equal("Invalid authorization context.", result.ErrorMessage);
        }

        [Theory]
        [DisplayName("Validate fails with InvalidRequest for a missing or blank ApiKey")]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_MissingApiKey_Fails(string apiKey)
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = apiKey,
                Method = "Foo.Bar",
                Authorization = "Bearer " + Guid.NewGuid()
            };

            var result = CreateValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Equal("Missing or invalid API key.", result.ErrorMessage);
        }

        [Theory]
        [DisplayName("Validate succeeds with an empty AccessToken for methods that need no authorization")]
        [InlineData("System.Ping")]
        [InlineData("System.GetApiPayloadOptions")]
        [InlineData("System.Login")]
        public void Validate_NoAuthMethod_SucceedsWithEmptyToken(string method)
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = method,
                Authorization = string.Empty
            };

            var result = CreateValidator().Validate(context);

            Assert.True(result.IsValid);
            Assert.Equal(Guid.Empty, result.AccessToken);
        }

        [Fact]
        [DisplayName("Validate fails when authorization is required but the Authorization header is missing")]
        public void Validate_MissingAuthorizationHeader_Fails()
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = string.Empty
            };

            var result = CreateValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Equal("Missing Authorization header.", result.ErrorMessage);
        }

        [Fact]
        [DisplayName("Validate fails when the Authorization scheme is not Bearer")]
        public void Validate_NonBearerAuthorization_Fails()
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = "Basic abc123"
            };

            var result = CreateValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Contains("Bearer", result.ErrorMessage);
        }

        [Fact]
        [DisplayName("Validate fails when the Bearer token is not a Guid")]
        public void Validate_InvalidBearerToken_Fails()
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = "Bearer not-a-guid"
            };

            var result = CreateValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Equal("Invalid access token.", result.ErrorMessage);
        }

        [Fact]
        [DisplayName("Validate succeeds with a differently cased Bearer prefix (OrdinalIgnoreCase)")]
        public void Validate_BearerPrefixCaseInsensitive_Succeeds()
        {
            var token = Guid.NewGuid();
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = "bearer " + token
            };

            var result = CreateValidator().Validate(context);

            Assert.True(result.IsValid);
            Assert.Equal(token, result.AccessToken);
        }

        [Fact]
        [DisplayName("Validate succeeds with a valid Bearer token and returns its AccessToken")]
        public void Validate_ValidBearerToken_Succeeds()
        {
            var token = Guid.NewGuid();
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = $"Bearer {token}"
            };

            var result = CreateValidator().Validate(context);

            Assert.True(result.IsValid);
            Assert.Equal(token, result.AccessToken);
            Assert.Equal(string.Empty, result.ErrorMessage);
        }
    }
}
