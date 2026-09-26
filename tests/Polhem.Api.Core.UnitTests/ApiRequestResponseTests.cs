using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Messages;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for ApiRequest/ApiResponse base classes and System Request/Response types.
    /// </summary>
    public class ApiRequestResponseTests
    {
        [Fact]
        [DisplayName("LoginRequest inherits ApiRequest and implements ILoginRequest")]
        public void LoginRequest_InheritsApiRequest_ImplementsInterface()
        {
            var request = new LoginRequest
            {
                UserId = "admin",
                Password = "pass123",
                ClientPublicKey = "key-data"
            };

            Assert.IsType<ApiRequest>(request, exactMatch: false);
            Assert.IsType<Polhem.Api.Contracts.System.ILoginRequest>(request, exactMatch: false);
            Assert.Equal("admin", request.UserId);
            Assert.Equal("pass123", request.Password);
        }

        [Fact]
        [DisplayName("LoginResponse inherits ApiResponse and implements ILoginResponse")]
        public void LoginResponse_InheritsApiResponse_ImplementsInterface()
        {
            var token = Guid.NewGuid();
            var response = new LoginResponse
            {
                AccessToken = token,
                UserId = "user1",
                UserName = "Test User"
            };

            Assert.IsType<ApiResponse>(response, exactMatch: false);
            Assert.IsType<Polhem.Api.Contracts.System.ILoginResponse>(response, exactMatch: false);
            Assert.Equal(token, response.AccessToken);
        }

        [Fact]
        [DisplayName("LoginRequest round-trips through MessagePack")]
        public void LoginRequest_MessagePackRoundTrip_PreservesData()
        {
            var original = new LoginRequest
            {
                UserId = "testUser",
                Password = "testPass",
                ClientPublicKey = "publicKeyPem"
            };

            var bytes = MessagePackCodec.Serialize(original);
            var deserialized = MessagePackCodec.Deserialize<LoginRequest>(bytes);

            Assert.Equal(original.UserId, deserialized.UserId);
            Assert.Equal(original.Password, deserialized.Password);
            Assert.Equal(original.ClientPublicKey, deserialized.ClientPublicKey);
        }

        [Fact]
        [DisplayName("LoginResponse round-trips through MessagePack")]
        public void LoginResponse_MessagePackRoundTrip_PreservesData()
        {
            var token = Guid.NewGuid();
            var expiredAt = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            var original = new LoginResponse
            {
                AccessToken = token,
                ExpiredAt = expiredAt,
                ApiEncryptionKey = "encrypted-key",
                UserId = "user1",
                UserName = "Test User"
            };

            var bytes = MessagePackCodec.Serialize(original);
            var deserialized = MessagePackCodec.Deserialize<LoginResponse>(bytes);

            Assert.Equal(token, deserialized.AccessToken);
            Assert.Equal(expiredAt, deserialized.ExpiredAt);
            Assert.Equal("encrypted-key", deserialized.ApiEncryptionKey);
            Assert.Equal("user1", deserialized.UserId);
            Assert.Equal("Test User", deserialized.UserName);
        }

        [Fact]
        [DisplayName("PingRequest round-trips through MessagePack")]
        public void PingRequest_MessagePackRoundTrip_PreservesData()
        {
            var original = new PingRequest
            {
                ClientName = "TestClient",
                TraceId = "trace-123"
            };

            var bytes = MessagePackCodec.Serialize(original);
            var deserialized = MessagePackCodec.Deserialize<PingRequest>(bytes);

            Assert.Equal("TestClient", deserialized.ClientName);
            Assert.Equal("trace-123", deserialized.TraceId);
        }
    }
}
