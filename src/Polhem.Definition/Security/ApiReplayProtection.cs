namespace Polhem.Definition.Security
{
    /// <summary>
    /// Whether an API method's calls must each carry a sequence number not seen before.
    /// </summary>
    /// <remarks>
    /// A third dimension alongside <see cref="ApiProtectionLevel"/> and
    /// <see cref="ApiAccessRequirement"/>, declared per method because replaying a read costs
    /// nothing while replaying a write does not.
    /// </remarks>
    public enum ApiReplayProtection
    {
        /// <summary>
        /// Calls are not checked for a repeated sequence number. The default, and correct for reads.
        /// </summary>
        None = 0,

        /// <summary>
        /// Each call must carry a sequence number this session has not used before.
        /// </summary>
        /// <remarks>
        /// <para>
        /// IMPORTANT: this protects Encrypted payloads only. Plain carries no frame, so a Plain call
        /// is not checked at all; Encoded frames are not authenticated — compression is not a MAC —
        /// so a captured Encoded call can be re-framed with a fresh sequence number and replayed.
        /// Only inside an Encrypted payload does the payload HMAC cover the frame. Leaving a method
        /// with this setting at <see cref="ApiProtectionLevel.Public"/> or
        /// <see cref="ApiProtectionLevel.Encoded"/> therefore leaves those formats unprotected,
        /// which this setting cannot close.
        /// </para>
        /// <para>
        /// It also requires the wire frame to be switched on (<c>ApiServiceOptions.RequireWireFrame</c>
        /// in <c>Polhem.Api.Core</c>), which is off by default; a host built with
        /// <c>AddPolhemFramework</c> logs a startup warning when methods declare this setting while
        /// the frame is off. Anonymous callers are not checked
        /// either: sequence numbers are per session, and calls made without one have no session to
        /// count against.
        /// </para>
        /// </remarks>
        UniqueSequence = 1
    }
}
