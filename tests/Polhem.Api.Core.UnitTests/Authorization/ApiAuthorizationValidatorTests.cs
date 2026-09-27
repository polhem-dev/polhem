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
        [DisplayName("Validate succeeds with an empty AccessToken for the anonymous methods when no Authorization header is sent")]
        [InlineData("System.Ping")]
        [InlineData("System.Login")]
        [InlineData("System.GetCommonConfiguration")]
        [InlineData("System.ExecFuncAnonymous")]
        [InlineData("Employee.ExecFuncAnonymous")]
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
        [DisplayName("Validate lets a request without an Authorization header through as anonymous, leaving the access decision to the executor")]
        public void Validate_MissingAuthorizationHeader_SucceedsAsAnonymous()
        {
            // This used to fail for every method outside a hand-kept list. The header was never
            // authentication at this layer — any well-formed token, the empty one included, passed — so
            // the list only disagreed with [ApiAccessControl]. ApiAccessValidator refuses an Authenticated
            // method called with the empty token (ApiAccessValidatorTests.ValidateAccess_Authenticated_EmptyToken_Throws).
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = string.Empty
            };

            var result = CreateValidator().Validate(context);

            Assert.True(result.IsValid);
            Assert.Equal(Guid.Empty, result.AccessToken);
        }

        [Fact]
        [DisplayName("Validate still refuses a missing Authorization header for a method a subclass declares as requiring one")]
        public void Validate_MissingAuthorizationHeader_RequiredBySubclass_Fails()
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = "Foo.Bar",
                Authorization = string.Empty
            };

            var result = new HeaderRequiredValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Equal("Missing Authorization header.", result.ErrorMessage);
        }

        [Theory]
        [DisplayName("Validate parses a present Authorization header for every method, anonymous ones included")]
        [InlineData("System.Ping")]
        [InlineData("System.Login")]
        [InlineData("Foo.Bar")]
        public void Validate_MalformedHeader_FailsForEveryMethod(string method)
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "test-key",
                Method = method,
                Authorization = "Bearer not-a-guid"
            };

            var result = CreateValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal("Invalid access token.", result.ErrorMessage);
        }

        private sealed class HeaderRequiredValidator : ApiAuthorizationValidator
        {
            protected override bool IsAuthorizationRequired(string method) => true;
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
