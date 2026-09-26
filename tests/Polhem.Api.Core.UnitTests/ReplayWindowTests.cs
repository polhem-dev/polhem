using System.Collections.Concurrent;
using System.ComponentModel;
using Polhem.Api.Core.JsonRpc;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests of the ReplayWindow sliding window algorithm (pure logic, no process-wide state).
    /// </summary>
    public class ReplayWindowTests
    {
        [Fact]
        [DisplayName("The first sequence number is accepted whatever its size and becomes the baseline")]
        public void TryAccept_FirstSequence_IsAccepted()
        {
            var window = new ReplayWindow();

            Assert.True(window.TryAccept(5_000));
        }

        [Fact]
        [DisplayName("A repeated sequence number is rejected")]
        public void TryAccept_RepeatedSequence_IsRejected()
        {
            var window = new ReplayWindow();
            window.TryAccept(10);

            Assert.False(window.TryAccept(10));
        }

        [Fact]
        [DisplayName("Increasing sequence numbers are all accepted")]
        public void TryAccept_IncreasingSequences_AllAccepted()
        {
            var window = new ReplayWindow();

            for (long i = 1; i <= 500; i++)
            {
                Assert.True(window.TryAccept(i), $"sequence {i} should be accepted");
            }
        }

        [Fact]
        [DisplayName("Out-of-order arrivals within the window are all accepted because concurrent senders do not guarantee arrival order")]
        public void TryAccept_OutOfOrderWithinWindow_AllAccepted()
        {
            // Taking a number is atomic, but when several connectors send concurrently the arrival order is not fixed.
            // A strictly increasing rule would reject this normal traffic, so the window must tolerate reordering.
            var window = new ReplayWindow();
            window.TryAccept(100);

            Assert.True(window.TryAccept(98));
            Assert.True(window.TryAccept(99));
            Assert.True(window.TryAccept(101));
        }

        [Fact]
        [DisplayName("An old sequence number behind the window is rejected")]
        public void TryAccept_SequenceBehindWindow_IsRejected()
        {
            var window = new ReplayWindow();
            window.TryAccept(ReplayWindow.WindowSize + 10);

            Assert.False(window.TryAccept(9));
        }

        [Fact]
        [DisplayName("A forward jump beyond the window width clears the bitmap so earlier sequence numbers can no longer be used")]
        public void TryAccept_JumpBeyondWindow_ClearsEarlierSlots()
        {
            var window = new ReplayWindow();
            window.TryAccept(1);
            window.TryAccept(2);

            Assert.True(window.TryAccept(2 + ReplayWindow.WindowSize));
            Assert.False(window.TryAccept(2));
        }

        [Fact]
        [DisplayName("A forward jump exactly at the limit is accepted")]
        public void TryAccept_JumpExactlyAtLimit_IsAccepted()
        {
            var window = new ReplayWindow();
            window.TryAccept(1);

            Assert.True(window.TryAccept(1 + ReplayWindow.MaxForwardJump));
        }

        [Fact]
        [DisplayName("A forward jump beyond the limit is rejected so one miscalculation cannot lock up the session")]
        public void TryAccept_JumpBeyondLimit_IsRejected()
        {
            // Without a limit, a single integer arithmetic mistake on the client that sends a number near `long.MaxValue`
            // would put every later normal request of that session outside the window. They would all fail with a valid
            // token and correct keys, which is extremely hard to diagnose.
            var window = new ReplayWindow();
            window.TryAccept(1);

            Assert.False(window.TryAccept(long.MaxValue));
            Assert.True(window.TryAccept(2));
        }

        [Fact]
        [DisplayName("A negative sequence number is rejected")]
        public void TryAccept_NegativeSequence_IsRejected()
        {
            var window = new ReplayWindow();

            Assert.False(window.TryAccept(-1));
        }

        [Fact]
        [DisplayName("The same sequence number sent concurrently is accepted exactly once")]
        public void TryAccept_SameSequenceConcurrently_AcceptedExactlyOnce()
        {
            // Concurrent requests of the same session share the window, so the read-modify-write must be atomic.
            var window = new ReplayWindow();
            var results = new ConcurrentBag<bool>();

            Parallel.For(0, 200, _ => results.Add(window.TryAccept(7)));

            Assert.Single(results, accepted => accepted);
        }

        [Fact]
        [DisplayName("Distinct sequence numbers within the window width sent concurrently are all accepted")]
        public void TryAccept_DistinctSequencesConcurrently_AllAccepted()
        {
            var window = new ReplayWindow();
            var results = new ConcurrentBag<bool>();

            Parallel.For(0, ReplayWindow.WindowSize, i => results.Add(window.TryAccept(i)));

            Assert.Equal(ReplayWindow.WindowSize, results.Count(accepted => accepted));
        }
    }
}
