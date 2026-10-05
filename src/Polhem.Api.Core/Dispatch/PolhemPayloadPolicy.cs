using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Validator;
using Polhem.Definition.Security;
using Polhem.JsonRpc.Payload.Server;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// Polhem's answers to the payload filter: the session key of the caller's access token, the access token of a
    /// remote call as the replay scope, <see cref="ApiReplayProtection.UniqueSequence"/> for the methods that reject a
    /// repeated sequence number, and the wire message type of the method's parameter.
    /// </summary>
    internal sealed class PolhemPayloadPolicy : IPayloadServerPolicy
    {
        public static PolhemPayloadPolicy Instance { get; } = new();

        private PolhemPayloadPolicy() { }

        public ValueTask<byte[]?> GetKeyAsync(JsonRpcRequestContext context)
        {
            var services = context.Services
                ?? throw new InvalidOperationException("A Polhem call needs the services of its scope.");
            var accessToken = PolhemCallState.Get(context).AccessToken;
            return ValueTask.FromResult<byte[]?>(services.GetRequiredService<IApiEncryptionKeyProvider>().GetKey(accessToken));
        }

        public string? GetReplayScope(JsonRpcRequestContext context)
        {
            // A local call never crossed a network, so there is nothing to replay. Giving it a scope would also make
            // a guarded method refuse it, because the local provider sends Plain outside debug mode. Anonymous calls
            // all share `Guid.Empty`, so a scope for them would let clients use up each other's sequence numbers.
            var state = PolhemCallState.Get(context);
            return state.IsLocalCall || state.AccessToken == Guid.Empty ? null : state.AccessToken.ToString("N");
        }

        public bool RequiresUniqueSequence(JsonRpcRequestContext context)
            => ApiAccessValidator.FindAccessControl(context.Method!.MethodInfo)?.ReplayProtection == ApiReplayProtection.UniqueSequence;

        public Type GetPayloadType(JsonRpcRequestContext context) => ActionPayloadType.Resolve(context.Method!.MethodInfo);
    }
}
