using System.ComponentModel;
using Polhem.Api.Core.Authorization;
using Polhem.Api.Core.JsonRpc;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.UnitTests.Authorization
{
    /// <summary>
    /// How <see cref="ApiAuthorizationValidator"/> decides on the API key validation result: the compatibility state
    /// keeps the presence check, the strict state allows or rejects according to the validation result, and the key
    /// exemption list contains only <c>System.Ping</c>.
    /// </summary>
    public class ApiKeyGateAuthorizationTests
    {
        private const string RejectedMessage = "Missing or invalid API key.";

        private static ApiAuthorizationContext NewContext(ApiKeyValidationResult validation,
            string apiKey = "some-key", string method = "Foo.Bar")
        {
            return new ApiAuthorizationContext
            {
                ApiKey = apiKey,
                Authorization = "Bearer " + Guid.NewGuid(),
                Method = method,
                ApiKeyValidation = validation,
            };
        }

        [Fact]
        [DisplayName("A valid key is allowed")]
        public void Validate_ValidKey_Succeeds()
        {
            var context = NewContext(new ApiKeyValidationResult(ApiKeyStatus.Valid, "app", "App"));

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.True(result.IsValid);
        }

        [Theory]
        [DisplayName("A missing or invalid key is rejected in the strict state with the same message")]
        [InlineData(ApiKeyStatus.NotProvided)]
        [InlineData(ApiKeyStatus.Invalid)]
        public void Validate_GateInForce_RejectedKey_FailsWithSameMessage(ApiKeyStatus status)
        {
            var context = NewContext(new ApiKeyValidationResult(status));

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(JsonRpcErrorCode.InvalidRequest, result.Code);
            Assert.Equal(RejectedMessage, result.ErrorMessage);
        }

        [Theory]
        [DisplayName("The compatibility state (no keys issued yet) and in-process calls keep the presence check: a non-empty key is allowed")]
        [InlineData(ApiKeyStatus.NotConfigured)]
        [InlineData(ApiKeyStatus.NotChecked)]
        public void Validate_GateNotInForce_NonEmptyKey_Succeeds(ApiKeyStatus status)
        {
            var context = NewContext(new ApiKeyValidationResult(status));

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.True(result.IsValid);
        }

        [Theory]
        [DisplayName("The compatibility state (no keys issued yet) and in-process calls keep the presence check: an empty key is rejected")]
        [InlineData(ApiKeyStatus.NotConfigured)]
        [InlineData(ApiKeyStatus.NotChecked)]
        public void Validate_GateNotInForce_EmptyKey_Fails(ApiKeyStatus status)
        {
            var context = NewContext(new ApiKeyValidationResult(status), apiKey: "  ");

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(RejectedMessage, result.ErrorMessage);
        }

        [Fact]
        [DisplayName("System.Ping needs no key: it is allowed without a key in the strict state")]
        public void Validate_Ping_NoKey_Succeeds()
        {
            var context = NewContext(new ApiKeyValidationResult(ApiKeyStatus.NotProvided),
                apiKey: string.Empty, method: "System.Ping");

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.True(result.IsValid);
        }

        [Fact]
        [DisplayName("System.Ping needs no key: it is allowed with a wrong key in the strict state (PingResult reports the status separately)")]
        public void Validate_Ping_InvalidKey_Succeeds()
        {
            var context = NewContext(new ApiKeyValidationResult(ApiKeyStatus.Invalid, "app", string.Empty),
                method: "System.Ping");

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.True(result.IsValid);
        }

        [Theory]
        [DisplayName("Needing no Bearer token does not mean needing no key: the anonymous methods other than Ping still need a key")]
        [InlineData("System.Login")]
        [InlineData("System.GetCommonConfiguration")]
        [InlineData("System.ExecFuncAnonymous")]
        public void Validate_BearerExemptMethods_StillRequireApiKey(string method)
        {
            var context = NewContext(new ApiKeyValidationResult(ApiKeyStatus.NotProvided),
                apiKey: string.Empty, method: method);

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.False(result.IsValid);
            Assert.Equal(RejectedMessage, result.ErrorMessage);
        }

        [Fact]
        [DisplayName("A null validation result falls back to the presence check of the compatibility state")]
        public void Validate_NullValidation_FallsBackToPresenceCheck()
        {
            var context = new ApiAuthorizationContext
            {
                ApiKey = "some-key",
                Authorization = "Bearer " + Guid.NewGuid(),
                Method = "Foo.Bar",
                ApiKeyValidation = null!,
            };

            var result = new ApiAuthorizationValidator().Validate(context);

            Assert.True(result.IsValid);
        }
    }
}
