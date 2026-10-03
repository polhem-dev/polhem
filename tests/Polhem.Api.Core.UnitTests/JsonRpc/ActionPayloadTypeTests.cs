using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages;
using Polhem.Api.Core.Transformers;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Business;
using Polhem.Business.System;
using Polhem.Definition.Filters;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// Tests for <see cref="ActionPayloadType"/>: the server, not the caller's type name, decides what an encoded
    /// request body is decoded into.
    /// </summary>
    public class ActionPayloadTypeTests
    {
        [Fact]
        [DisplayName("Resolve maps a business-layer argument to the framework request type it shares a contract with")]
        public void Resolve_BusinessArgs_ReturnsRequestCounterpart()
        {
            var method = typeof(SystemBusinessObject).GetMethod(nameof(SystemBusinessObject.Login))!;

            Assert.Equal(typeof(LoginRequest), ActionPayloadType.Resolve(method));
        }

        [Fact]
        [DisplayName("Resolve gives every framework action a request type, so a .NET client's request always matches")]
        public void Resolve_EveryFrameworkAction_ReturnsAnApiRequest()
        {
            var actions = typeof(BusinessObject).Assembly.GetTypes()
                .Where(t => typeof(BusinessObject).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(Polhem.JsonRpc.Server.JsonRpcMethod.IsResolvableAction)
                .ToList();
            Assert.NotEmpty(actions);

            var unmatched = actions
                .Where(m => !typeof(ApiRequest).IsAssignableFrom(ActionPayloadType.Resolve(m)))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}({m.GetParameters()[0].ParameterType.Name})")
                .ToList();

            Assert.True(unmatched.Count == 0,
                "These actions decode an encoded body into their business-layer argument, which no framework client sends: "
                + string.Join(", ", unmatched));
        }

        [Fact]
        [DisplayName("Resolve keeps a parameter that is itself a request type")]
        public void Resolve_RequestParameter_ReturnsItself()
        {
            var method = typeof(TakesRequest).GetMethod(nameof(TakesRequest.Accept))!;

            Assert.Equal(typeof(PingRequest), ActionPayloadType.Resolve(method));
        }

        [Fact]
        [DisplayName("Resolve keeps a parameter that has no framework counterpart")]
        public void Resolve_ApplicationArgs_ReturnsParameterType()
        {
            var method = typeof(TakesRequest).GetMethod(nameof(TakesRequest.Custom))!;

            Assert.Equal(typeof(CustomArgs), ActionPayloadType.Resolve(method));
        }

        [Theory]
        [InlineData("Polhem.Api.Core.Messages.System.PingRequest, Polhem.Api.Core")]
        [InlineData("Polhem.Api.Core.Messages.System.PingRequest, polhem.api.core")]
        [InlineData("Polhem.Api.Core.Messages.System.PingRequest, Polhem.Api.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
        [InlineData("Polhem.Api.Core.Messages.System.PingRequest")]
        [InlineData("  Polhem.Api.Core.Messages.System.PingRequest ,  Polhem.Api.Core ")]
        [DisplayName("IsNamedBy accepts the type's own name, with or without its assembly")]
        public void IsNamedBy_MatchingName_ReturnsTrue(string typeName)
        {
            Assert.True(ActionPayloadType.IsNamedBy(typeName, typeof(PingRequest)));
        }

        [Theory]
        [InlineData("Polhem.Definition.Filters.FilterGroup, Polhem.Definition")]
        [InlineData("Polhem.Api.Core.Messages.System.PingRequest, Polhem.Definition")]
        [InlineData("polhem.api.core.messages.system.pingrequest, Polhem.Api.Core")]
        [InlineData("Polhem.Api.Core.Messages.System.PingRequestX, Polhem.Api.Core")]
        [InlineData("")]
        [DisplayName("IsNamedBy refuses any other type, and the right type claimed from another assembly")]
        public void IsNamedBy_OtherName_ReturnsFalse(string typeName)
        {
            Assert.False(ActionPayloadType.IsNamedBy(typeName, typeof(PingRequest)));
        }

        [Fact]
        [DisplayName("The server refuses a body whose declared type is not the method's, without decoding it")]
        public void Open_MismatchedTypeName_ThrowsBeforeDecoding()
        {
            // A whitelisted type the caller used to be able to choose freely — and the carrier of the nested
            // filter that once crashed the server from an anonymous call to Ping.
            var processor = new Polhem.JsonRpc.Payload.PayloadProcessor(PolhemPayload.CreateOptions());
            var envelope = processor.Seal(new FilterGroup(LogicalOperator.And), Polhem.JsonRpc.Payload.PayloadFormat.Encoded);

            var ex = Assert.Throws<InvalidOperationException>(() => processor.Open(envelope, typeof(PingRequest), null, out _));

            Assert.Contains("does not match", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The server decodes a matching body into the type it chose")]
        public void Open_MatchingTypeName_DecodesIntoTargetType()
        {
            var processor = new Polhem.JsonRpc.Payload.PayloadProcessor(PolhemPayload.CreateOptions());
            var envelope = processor.Seal(new GetListRequest { SelectFields = "sys_id" }, Polhem.JsonRpc.Payload.PayloadFormat.Encoded);

            var request = Assert.IsType<GetListRequest>(processor.Open(envelope, typeof(GetListRequest), null, out _));

            Assert.Equal("sys_id", request.SelectFields);
        }

        private sealed class TakesRequest
        {
            public string Last { get; private set; } = string.Empty;

            public void Accept(PingRequest request) => Last = request.TraceId ?? string.Empty;

            public void Custom(CustomArgs args) => Last = args.Name;
        }

        private sealed class CustomArgs
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
