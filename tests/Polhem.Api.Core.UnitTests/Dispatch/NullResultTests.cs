using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Contracts.System;
using Polhem.Api.Core.Messages.System;
using Polhem.Api.Core.Transformers;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;
using PayloadFormat = Polhem.Api.Core.Messages.PayloadFormat;

namespace Polhem.Api.Core.UnitTests.Dispatch
{
    /// <summary>
    /// Tests that a method returning null answers in the format of its request, and that the answer reads back as null.
    /// </summary>
    /// <remarks>
    /// The result of a call is sealed in the format the call was sent in, a null one included, and the reader refuses a
    /// result in another format. <see cref="TestDispatcher"/> opens the result the way the client connector does.
    /// </remarks>
    public class NullResultTests
    {
        private static readonly byte[] s_key = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

        private static TestDispatcher NewDispatcher(bool requireFrame, NullBusinessObject businessObject)
        {
            var services = TestDispatcher.AddDispatchDefaults(new ServiceCollection());
            var options = PolhemPayload.CreateOptions();
            options.RequireFrame = requireFrame;
            services.AddSingleton(options);
            services.AddSingleton<IBusinessObjectFactory>(new SingleObjectFactory(businessObject));
            services.AddSingleton<IAccessTokenValidator>(new RejectAllTokens());
            services.AddSingleton<IApiEncryptionKeyProvider>(new FixedKey());
            return new TestDispatcher(services.BuildServiceProvider()) { IsLocalCall = false };
        }

        [Theory]
        [DisplayName("A method that returns null answers in the request's format and reads back as null")]
        [InlineData(PayloadFormat.Plain, false)]
        [InlineData(PayloadFormat.Encoded, false)]
        [InlineData(PayloadFormat.Encrypted, false)]
        [InlineData(PayloadFormat.Plain, true)]
        [InlineData(PayloadFormat.Encoded, true)]
        [InlineData(PayloadFormat.Encrypted, true)]
        public async Task Execute_MethodReturningNull_AnswersNullInRequestFormat(PayloadFormat format, bool requireFrame)
        {
            var request = new TestRpcRequest
            {
                Method = $"Null.{nameof(NullBusinessObject.Nothing)}",
                Params = new TestPayload { Format = format, Value = new PingRequest(), Key = s_key },
            };

            var businessObject = new NullBusinessObject();

            var response = await NewDispatcher(requireFrame, businessObject).ExecuteAsync(request);

            Assert.Equal(1, businessObject.Calls);
            Assert.Null(response.Error);
            Assert.Equal(format, response.Result!.Format);
            Assert.Null(response.Result.Value);
        }

        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
        public sealed class NullBusinessObject
        {
            public int Calls { get; private set; }

            public PingResponse? Nothing(NullArgs args)
            {
                Calls++;
                return null;
            }
        }

        public sealed class NullArgs : IPingRequest
        {
            public string? ClientName { get; set; }

            public string? TraceId { get; set; }
        }

        private sealed class SingleObjectFactory(object businessObject) : IBusinessObjectFactory
        {
            public object CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall) => businessObject;
        }

        private sealed class RejectAllTokens : IAccessTokenValidator
        {
            public bool Validate(Guid accessToken) => false;
        }

        private sealed class FixedKey : IApiEncryptionKeyProvider
        {
            public byte[] GetKey(Guid accessToken) => s_key;

            public byte[] GenerateKeyForLogin(Guid accessToken) => s_key;

            public bool SupportsSessionRebuild => true;
        }
    }
}
