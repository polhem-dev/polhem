using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Polhem.Api.Contracts.System;
using Polhem.Api.Core.JsonRpc;
using Polhem.Business;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// Pins what a <c>Plain</c> request body can set on a business-object argument: only the members of the
    /// wire message the method's argument corresponds to, the same members an encoded body can set.
    /// </summary>
    /// <remarks>
    /// A <c>Plain</c> body used to be deserialized straight into the method's parameter type, so a caller could
    /// set any public setter of a business-layer argument, while an encoded body is decoded into the wire
    /// message and copied across. The gate test keeps the framework's own arguments free of members their
    /// contract does not declare, so the copy never has one to carry.
    /// </remarks>
    public class PlainBindingTests
    {
        [Fact]
        [DisplayName("A Plain body cannot set an argument member that its wire message does not carry")]
        public async Task Execute_PlainBodyWithExtraMember_DoesNotSetIt()
        {
            var bo = new ProbeBusinessObject();
            using var document = JsonDocument.Parse("""{"traceId":"t-1","elevated":true}""");
            var request = new JsonRpcRequest
            {
                Method = $"Probe.{nameof(ProbeBusinessObject.Probe)}",
                Params = new JsonRpcParams { Value = document.RootElement.Clone() },
                Id = "1",
            };
            var executor = new JsonRpcExecutor(new SingleObjectFactory(bo), new RejectAllTokens(), new NoKeys())
            {
                AccessToken = Guid.Empty,
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Null(response.Error);
            var args = Assert.IsType<ProbeArgs>(bo.Received);
            Assert.Equal("t-1", args.TraceId);
            Assert.False(args.Elevated);
        }

        [Fact]
        [DisplayName("A Plain body that cannot be read into the method's type is answered with InvalidParams (-32602) and a fixed message")]
        public async Task Execute_UnreadablePlainBody_ReturnsInvalidParams()
        {
            var bo = new ProbeBusinessObject();
            using var document = JsonDocument.Parse("""{"traceId":{"nested":1}}""");
            var request = new JsonRpcRequest
            {
                Method = $"Probe.{nameof(ProbeBusinessObject.Probe)}",
                Params = new JsonRpcParams { Value = document.RootElement.Clone() },
                Id = "1",
            };
            var executor = new JsonRpcExecutor(new SingleObjectFactory(bo), new RejectAllTokens(), new NoKeys())
            {
                AccessToken = Guid.Empty,
            };

            var response = await executor.ExecuteAsync(request);

            Assert.Equal((int)JsonRpcErrorCode.InvalidParams, response.Error!.Code);
            Assert.Null(bo.Received);
        }

        [Fact]
        [DisplayName("Every framework business-object action argument declares only its contract interface's members and Parameters")]
        public void FrameworkActionArguments_DeclareOnlyContractMembers()
        {
            var contractsAssembly = typeof(Polhem.Api.Contracts.IExecFuncRequest).Assembly;
            var argumentTypes = typeof(BusinessObject).Assembly.GetTypes()
                .Where(t => typeof(BusinessObject).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(JsonRpcExecutor.IsResolvableAction)
                .Select(m => m.GetParameters()[0].ParameterType)
                .Where(t => typeof(BusinessArgs).IsAssignableFrom(t))
                .Distinct()
                .ToList();
            Assert.NotEmpty(argumentTypes);

            var offenders = new List<string>();
            foreach (var type in argumentTypes)
            {
                var contractMembers = type.GetInterfaces()
                    .Where(i => i.Assembly == contractsAssembly)
                    .SelectMany(i => i.GetProperties())
                    .Select(p => p.Name)
                    .Append(nameof(BusinessArgs.Parameters))
                    .ToHashSet(StringComparer.Ordinal);
                offenders.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && !contractMembers.Contains(p.Name))
                    .Select(p => $"{type.Name}.{p.Name}"));
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// An argument that speaks for <see cref="IPingRequest"/> but carries a member of its own, the shape an
        /// application argument could take.
        /// </summary>
        public sealed class ProbeArgs : IPingRequest
        {
            public string? ClientName { get; set; }

            public string? TraceId { get; set; }

            public bool Elevated { get; set; }
        }

        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
        public sealed class ProbeBusinessObject
        {
            public ProbeArgs? Received { get; private set; }

            public string Probe(ProbeArgs args)
            {
                Received = args;
                return args.TraceId ?? string.Empty;
            }
        }

        private sealed class SingleObjectFactory(object businessObject) : IBusinessObjectFactory
        {
            public object CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall) => businessObject;
        }

        private sealed class RejectAllTokens : IAccessTokenValidator
        {
            public bool Validate(Guid accessToken) => false;
        }

        private sealed class NoKeys : IApiEncryptionKeyProvider
        {
            public byte[] GetKey(Guid accessToken) => [];

            public byte[] GenerateKeyForLogin(Guid accessToken) => [];

            public bool SupportsSessionRebuild => false;
        }
    }
}
