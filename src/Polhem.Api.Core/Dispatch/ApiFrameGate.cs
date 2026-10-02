using System.Reflection;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Validator;
using Polhem.Definition.Security;

namespace Polhem.Api.Core.Dispatch
{
    /// <summary>
    /// The replay-protection checks on the wire frame of a restored payload (adr-042).
    /// </summary>
    internal static class ApiFrameGate
    {
        public static void ValidateTimestamp(ApiPayloadFrame? frame)
        {
            if (frame == null) { return; }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long driftMs = Math.Abs(nowMs - frame.TimestampMs);
            double toleranceMs = ApiServiceOptions.WireFrameTimestampTolerance.TotalMilliseconds;
            if (driftMs > toleranceMs)
            {
                throw new ReplayRejectedException(
                    $"The request timestamp is {driftMs / 1000} seconds away from server time, outside the accepted window. Check the client clock.");
            }
        }

        public static async ValueTask ValidateSequenceAsync(MethodInfo method, ApiPayloadFrame? frame, Guid accessToken, CancellationToken cancellationToken)
        {
            if (frame == null || accessToken == Guid.Empty) { return; }
            var attr = ApiAccessValidator.FindAccessControl(method);
            if (attr?.ReplayProtection != ApiReplayProtection.UniqueSequence) { return; }

            bool accepted = await ApiServiceOptions.ReplayWindowStore
                .TryAcceptAsync(accessToken, frame.Sequence, cancellationToken).ConfigureAwait(false);
            if (!accepted)
            {
                throw new ReplayRejectedException(
                    "This request repeats a sequence number the session has already used, or falls outside the accepted range.");
            }
        }
    }
}
