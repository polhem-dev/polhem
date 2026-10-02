using System.Diagnostics;
using Polhem.Api.Core.JsonRpc;
using Polhem.Definition.Security;
using Polhem.JsonRpc.Server;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// What the Polhem framework learns about one call as it passes through the dispatcher: who is calling, through
    /// which transport, and the payload once it is restored.
    /// </summary>
    internal sealed class PolhemCallState
    {
        private const string ItemKey = "Polhem.Api.Core.Dispatch.CallState";

        public Guid AccessToken { get; set; }

        public bool IsLocalCall { get; set; }

        public ApiKeyValidationResult ApiKeyValidation { get; set; } = ApiKeyValidationResult.NotChecked;

        public Stopwatch? Stopwatch { get; set; }

        public JsonRpcParams? Payload { get; set; }

        public byte[]? EncryptionKey { get; set; }

        public static PolhemCallState Create(JsonRpcRequestContext context)
        {
            var state = new PolhemCallState();
            context.Items[ItemKey] = state;
            return state;
        }

        public static PolhemCallState? Find(JsonRpcRequestContext context) =>
            context.Items.TryGetValue(ItemKey, out var value) ? value as PolhemCallState : null;

        public static PolhemCallState Get(JsonRpcRequestContext context) =>
            Find(context) ?? throw new InvalidOperationException("The Polhem object factory did not run for this call.");
    }
}
