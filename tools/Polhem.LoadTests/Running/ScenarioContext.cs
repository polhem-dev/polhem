namespace Polhem.LoadTests.Running
{
    /// <summary>
    /// Per-virtual-user state handed to a scenario on each call.
    /// </summary>
    /// <param name="VirtualUserIndex">Zero-based index of the calling virtual user.</param>
    /// <param name="Iteration">Zero-based iteration number within that virtual user.</param>
    /// <remarks>
    /// IMPORTANT: whatever a scenario needs per user — a session, a connector — belongs on the
    /// instance this context identifies, never on shared state. A <c>PolhemApiClient</c> holds one
    /// signed-in identity, so virtual users that signed in through a shared one would overwrite each
    /// other's tokens and transmission keys, and the symptom is not a clear error but decryption
    /// failures that read like framework instability.
    /// </remarks>
    public readonly record struct ScenarioContext(int VirtualUserIndex, long Iteration);
}
