namespace Polhem.Api.Core.JsonRpc
{
    /// <summary>
    /// Decides, per session, whether a request's sequence number may be accepted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The store makes the accept-or-reject decision itself rather than handing out a window for
    /// the caller to consult, so the decision is made wherever the store keeps its state. The
    /// default, <see cref="MemoryReplayWindowStore"/>, keeps it in this process's memory: with
    /// several nodes behind a load balancer and no token affinity, each node decides on its own,
    /// so a captured packet can be replayed once per node. That is a far smaller exposure than an
    /// unbounded replay. A deployment that cannot accept it implements this interface over a
    /// shared store (a cache or a database), where the decision is one atomic operation on the
    /// shared state.
    /// </para>
    /// <para>
    /// An implementation should follow the rules of <see cref="MemoryReplayWindowStore"/>: accept
    /// each sequence once per session, tolerate out-of-order arrival within
    /// <see cref="MemoryReplayWindowStore.WindowSize"/> slots below the highest sequence seen, and
    /// refuse a jump of more than <see cref="MemoryReplayWindowStore.MaxForwardJump"/> above it.
    /// Honest clients send several requests at once on one session, so a strict "must be greater
    /// than the last" rule would reject legitimate traffic.
    /// </para>
    /// </remarks>
    public interface IReplayWindowStore
    {
        /// <summary>
        /// Records <paramref name="sequence"/> for the session and reports whether it is acceptable.
        /// </summary>
        /// <param name="accessToken">The session's access token.</param>
        /// <param name="sequence">The sequence number carried by the request.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// <c>true</c> when the session has not used the number and it lies within the window;
        /// <c>false</c> when it repeats one already used or falls outside the window.
        /// </returns>
        /// <remarks>
        /// IMPORTANT: checking and recording must be one atomic step. Two concurrent requests
        /// carrying the same sequence must not both be accepted, which a separate read followed by
        /// a write allows.
        /// </remarks>
        ValueTask<bool> TryAcceptAsync(Guid accessToken, long sequence, CancellationToken cancellationToken = default);
    }
}
