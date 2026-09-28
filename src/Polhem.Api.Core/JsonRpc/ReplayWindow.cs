namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// A sliding window of recently seen sequence numbers for one session, accepting each number
    /// once and tolerating out-of-order arrival.
    /// </summary>
    /// <remarks>
    /// This is the IPsec anti-replay window (RFC 6479) in miniature: the highest sequence seen so
    /// far, plus a 64-bit map of which of the preceding 64 slots have been used. Sixteen bytes per
    /// session and a few bit operations per call, so the check costs no database round trip.
    /// <para>
    /// Tolerating out-of-order arrival is required, not a nicety: clients hand out sequence numbers
    /// atomically but several requests can be in flight at once, so the order they arrive in is not
    /// the order they were numbered in. A strict "must be greater than the last" rule would reject
    /// honest traffic.
    /// </para>
    /// <para>
    /// Instances are shared across concurrent requests on one session, so every read-modify-write
    /// runs under the instance lock.
    /// </para>
    /// <para>
    /// Internal because it is how <see cref="MemoryReplayWindowStore"/> keeps its state, not part
    /// of the <see cref="IReplayWindowStore"/> contract: a shared store keeps its own state and
    /// makes the decision there.
    /// </para>
    /// </remarks>
    internal sealed class ReplayWindow
    {
        /// <summary>The number of preceding sequence slots the window remembers.</summary>
        public const int WindowSize = MemoryReplayWindowStore.WindowSize;

        /// <summary>
        /// How far above the highest seen sequence a new one may jump before it is refused.
        /// </summary>
        public static readonly long MaxForwardJump = MemoryReplayWindowStore.MaxForwardJump;

        private readonly Lock _gate = new();
        private long _highest = -1;
        private ulong _seen;

        /// <summary>
        /// Records <paramref name="sequence"/> and reports whether it is acceptable.
        /// </summary>
        /// <param name="sequence">The sequence number carried by the request.</param>
        /// <returns>
        /// <c>true</c> when the number has not been seen and lies within the window; <c>false</c>
        /// when it repeats one already seen, has fallen out of the back of the window, or jumps
        /// further ahead than <see cref="MaxForwardJump"/>.
        /// </returns>
        public bool TryAccept(long sequence)
        {
            if (sequence < 0) { return false; }

            lock (_gate)
            {
                if (_highest < 0)
                {
                    // First request on this session sets the baseline.
                    _highest = sequence;
                    _seen = 1UL;
                    return true;
                }

                if (sequence > _highest)
                {
                    long advance = sequence - _highest;
                    if (advance > MaxForwardJump) { return false; }

                    _seen = advance >= WindowSize ? 0UL : _seen << (int)advance;
                    _seen |= 1UL;
                    _highest = sequence;
                    return true;
                }

                long behind = _highest - sequence;
                if (behind >= WindowSize) { return false; }

                ulong bit = 1UL << (int)behind;
                if ((_seen & bit) != 0) { return false; }

                _seen |= bit;
                return true;
            }
        }
    }
}
