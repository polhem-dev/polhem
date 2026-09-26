using System.ComponentModel;
using Polhem.Api.Core.Validator;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    public class ApiAccessValidatorTests
    {
        private sealed class FakeTokenProvider : IAccessTokenValidator
        {
            public bool Result { get; init; }
            public bool Validate(Guid accessToken) => Result;
        }

        // Tests that don't exercise the token-validator branch pass a "deny-all" stub by default;
        // never reached for paths that short-circuit before the token check.
        private static readonly FakeTokenProvider s_denyAll = new() { Result = false };

        [Fact]
        [DisplayName("ValidateAccess throws UnauthorizedAccessException when the method has no ApiAccessControl")]
        public void ValidateAccess_NoAttribute_Throws()
        {
            var method = typeof(DummyApi).GetMethod(nameof(DummyApi.Method_NoAttribute));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Encrypted,
                IsLocalCall = false,
                AccessToken = Guid.NewGuid()
            };

            Assert.Throws<UnauthorizedAccessException>(() =>
                ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
        }

        [Fact]
        [DisplayName("ValidateAccess throws when Authenticated is required and the AccessToken is empty")]
        public void ValidateAccess_Authenticated_EmptyToken_Throws()
        {
            var method = typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Authenticated));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Encrypted,
                IsLocalCall = false,
                AccessToken = Guid.Empty
            };

            Assert.Throws<UnauthorizedAccessException>(() =>
                ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
        }

        [Fact]
        [DisplayName("ValidateAccess throws when Authenticated is required and the provider returns false")]
        public void ValidateAccess_Authenticated_InvalidToken_Throws()
        {
            var fake = new FakeTokenProvider { Result = false };
            var method = typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Authenticated));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Encrypted,
                IsLocalCall = false,
                AccessToken = Guid.NewGuid()
            };

            Assert.Throws<UnauthorizedAccessException>(() =>
                ApiAccessValidator.ValidateAccess(method!, context, fake));
        }

        [Fact]
        [DisplayName("ValidateAccess passes when Authenticated is required and the provider returns true")]
        public void ValidateAccess_Authenticated_ValidToken_Succeeds()
        {
            var fake = new FakeTokenProvider { Result = true };
            var method = typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Authenticated));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Encrypted,
                IsLocalCall = false,
                AccessToken = Guid.NewGuid()
            };

            var ex = Record.Exception(() => ApiAccessValidator.ValidateAccess(method!, context, fake));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ValidateAccess throws ArgumentNullException for a null tokenValidator")]
        public void ValidateAccess_NullTokenValidator_Throws()
        {
            var method = typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Authenticated));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Encrypted,
                IsLocalCall = false,
                AccessToken = Guid.NewGuid()
            };

            Assert.Throws<ArgumentNullException>(() =>
                ApiAccessValidator.ValidateAccess(method!, context, null!));
        }

        [Theory]
        [DisplayName("ValidateAccess validates access according to the protection level and payload format")]
        [InlineData(ApiProtectionLevel.Public, PayloadFormat.Plain, true)]                      // Remote call, Public API, Plain transport → allowed
        [InlineData(ApiProtectionLevel.Encoded, PayloadFormat.Encoded, true)]                  // Remote call, Encoded API, Encoded transport → allowed
        [InlineData(ApiProtectionLevel.Encoded, PayloadFormat.Plain, false)]                   // Remote call, Encoded API, Plain transport → rejected (not encoded)
        [InlineData(ApiProtectionLevel.Encrypted, PayloadFormat.Encrypted, true)]              // Remote call, Encrypted API, Encrypted transport → allowed
        [InlineData(ApiProtectionLevel.Encrypted, PayloadFormat.Encoded, false)]               // Remote call, Encrypted API, Encoded transport → rejected (not encrypted)
        [InlineData(ApiProtectionLevel.LocalOnly, PayloadFormat.Plain, true, true)]            // Local call, LocalOnly API, Plain transport → allowed (no format restriction locally)
        [InlineData(ApiProtectionLevel.Encrypted, PayloadFormat.Plain, true, true)]            // Local call, Encrypted API, Plain transport → allowed (no encryption requirement locally)
        [InlineData(ApiProtectionLevel.Encoded, PayloadFormat.Plain, true, true)]              // Local call, Encoded API, Plain transport → allowed (no encoding requirement locally)
        [InlineData(ApiProtectionLevel.Public, PayloadFormat.Plain, true, true)]               // Local call, Public API, Plain transport → allowed
        public void ValidateAccess_VariousFormats_ValidatesCorrectly(
            ApiProtectionLevel protectionLevel,
            PayloadFormat format,
            bool expectedSuccess,
            bool isLocal = false)
        {
            // Arrange
            var method = protectionLevel switch
            {
                ApiProtectionLevel.Public => typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Public)),
                ApiProtectionLevel.Encoded => typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Encoded)),
                ApiProtectionLevel.Encrypted => typeof(DummyApi).GetMethod(nameof(DummyApi.Method_Encrypted)),
                ApiProtectionLevel.LocalOnly => typeof(DummyApi).GetMethod(nameof(DummyApi.Method_LocalOnly)),
                _ => throw new InvalidOperationException("Unknown protection level")
            };

            var context = new ApiCallContext
            {
                Format = format,
                IsLocalCall = isLocal,
                AccessToken = Guid.NewGuid()
            };

            // Act & Assert
            if (expectedSuccess)
            {
                var ex = Record.Exception(() => ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
                Assert.Null(ex);
            }
            else
            {
                Assert.Throws<UnauthorizedAccessException>(() =>
                    ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
            }
        }

        [Fact]
        [DisplayName("ValidateAccess uses the base method attribute when an override has none")]
        public void ValidateAccess_BaseMethodAttribute_InheritedByOverride_Succeeds()
        {
            var method = typeof(DerivedApi).GetMethod(nameof(DerivedApi.Method_Override));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Plain,
                IsLocalCall = false,
                AccessToken = Guid.Empty
            };

            var ex = Record.Exception(() => ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ValidateAccess uses the class-level attribute when the method has none")]
        public void ValidateAccess_ClassLevelAttribute_UsedWhenMethodHasNone_Succeeds()
        {
            var method = typeof(ClassLevelApi).GetMethod(nameof(ClassLevelApi.Method_NoAttribute));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Plain,
                IsLocalCall = false,
                AccessToken = Guid.Empty
            };

            var ex = Record.Exception(() => ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
            Assert.Null(ex);
        }

        [Fact]
        [DisplayName("ValidateAccess lets a method attribute override the class-level attribute")]
        public void ValidateAccess_MethodAttributeOverridesClassAttribute_MethodWins()
        {
            // The class is Public and the method is Encrypted, so Plain transport is rejected because of the method attribute.
            var method = typeof(ClassLevelApi).GetMethod(nameof(ClassLevelApi.Method_WithAttribute));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Plain,
                IsLocalCall = false,
                AccessToken = Guid.Empty
            };

            Assert.Throws<UnauthorizedAccessException>(() =>
                ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
        }

        [Fact]
        [DisplayName("ValidateAccess throws UnauthorizedAccessException for a remote call to a LocalOnly API")]
        public void ValidateAccess_LocalOnlyApi_RemoteCall_Throws()
        {
            var method = typeof(DummyApi).GetMethod(nameof(DummyApi.Method_LocalOnly));
            var context = new ApiCallContext
            {
                Format = PayloadFormat.Plain,
                IsLocalCall = false,
                AccessToken = Guid.Empty
            };

            Assert.Throws<UnauthorizedAccessException>(() =>
                ApiAccessValidator.ValidateAccess(method!, context, s_denyAll));
        }

        private class DummyApi
        {
            [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
            public static void Method_Public() { }

            [ApiAccessControl(ApiProtectionLevel.Encoded, ApiAccessRequirement.Anonymous)]
            public static void Method_Encoded() { }

            [ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Anonymous)]
            public static void Method_Encrypted() { }

            [ApiAccessControl(ApiProtectionLevel.LocalOnly, ApiAccessRequirement.Anonymous)]
            public static void Method_LocalOnly() { }

            public static void Method_NoAttribute() { }

            [ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated)]
            public static void Method_Authenticated() { }
        }

        private class BaseApi
        {
            [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
            public virtual void Method_Override() { }
        }

        private class DerivedApi : BaseApi
        {
            // No `[ApiAccessControl]` here, so the attribute of `BaseApi.Method_Override` applies.
            public override void Method_Override() { }
        }

        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
        private class ClassLevelApi
        {
            // No method-level attribute, so the class-level Public + Anonymous applies.
            public static void Method_NoAttribute() { }

            // The method-level attribute overrides the class-level one.
            [ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Anonymous)]
            public static void Method_WithAttribute() { }
        }
    }
}
