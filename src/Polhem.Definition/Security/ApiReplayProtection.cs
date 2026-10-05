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
        /// IMPORTANT: only an Encrypted payload can be checked. Plain carries no frame, and Encoded frames are not
        /// authenticated (compression is not a MAC), so a captured Encoded call can be re-framed with a fresh sequence
        /// number. Only inside an Encrypted payload does the payload HMAC cover the frame. Therefore, while the wire
        /// frame is required, a remote call from a signed-in session to a method with this setting is refused with
        /// <c>-32602 InvalidParams</c> unless it is Encrypted, whatever the method's <see cref="ApiProtectionLevel"/>.
        /// The refusal is made by the payload filter of Polhem.JsonRpc.Payload, for every call to which the framework's
        /// <c>PolhemPayloadPolicy</c> gives a replay scope.
        /// </para>
        /// <para>
        /// That policy gives no scope to in-process calls or to anonymous ones, so neither is checked or refused on this
        /// account. An in-process call never crossed a network, so there is nothing to replay. An anonymous call has no
        /// session to count sequence numbers against, so declaring this setting on a method that admits anonymous
        /// callers protects nothing.
        /// </para>
        /// <para>
        /// The wire frame is switched on with <c>PayloadOptions.RequireFrame</c> of Polhem.JsonRpc.Payload, set with
        /// <c>AddPolhemPayload</c>, and is off by default. While it is off nothing is checked; a host built with
        /// <c>AddPolhemFramework</c> logs a startup warning when methods declare this setting.
        /// </para>
        /// </remarks>
        UniqueSequence = 1
    }
}
