using System.ComponentModel;
using System.Reflection;
using Polhem.Api.Core.JsonRpc;
using Microsoft.Extensions.Time.Testing;

namespace Polhem.Api.Core.UnitTests.JsonRpc
{
    /// <summary>
    /// <see cref="MemoryReplayWindowStore"/>: the replay window store that ships as the default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type previously had **no tests**: nothing under `tests/` referenced it or <c>IReplayWindowStore</c>,
    /// although it is the per-session map every default deployment uses. It even exposes <c>Count</c> with the note
    /// "intended for tests and diagnostics", yet no test used it.
    /// </para>
    /// <para>
    /// Time always comes from <c>FakeTimeProvider</c> (through the <c>internal</c> constructor; this assembly is
    /// declared in <c>InternalsVisibleTo</c>), so this class **modifies no production static** and does not need the
    /// <c>ApiServiceOptionsState</c> collection. The eviction lifetime is derived from the current
    /// <see cref="ApiServiceOptions.WireFrameTimestampTolerance"/> and then passed with <c>Advance</c>, without
    /// spending any real time. That is also what makes the eviction boundary and the sweep throttle precisely assertable.
    /// </para>
    /// </remarks>
    public class MemoryReplayWindowStoreTests
    {
        /// <summary>The eviction lifetime, derived the same way as in the type under test so the test does not hard-code a second number.</summary>
        private static TimeSpan Lifetime => ApiServiceOptions.WireFrameTimestampTolerance * 2;

        [Fact]
        [DisplayName("GetOrAdd returns the same window for the same token so the sequence history is kept")]
        public void GetOrAdd_SameToken_ReturnsSameWindow()
        {
            var store = new MemoryReplayWindowStore();
            var token = Guid.NewGuid();

            var first = store.GetOrAdd(token);
            Assert.True(first.TryAccept(7));

            var second = store.GetOrAdd(token);

            Assert.Same(first, second);
            // Only a kept history blocks a replay, which is the point of returning the same instance.
            Assert.False(second.TryAccept(7));
        }

        [Fact]
        [DisplayName("Windows of different tokens are isolated from each other")]
        public void GetOrAdd_DifferentTokens_AreIsolated()
        {
            var store = new MemoryReplayWindowStore();

            Assert.True(store.GetOrAdd(Guid.NewGuid()).TryAccept(1));
            Assert.True(store.GetOrAdd(Guid.NewGuid()).TryAccept(1));
            Assert.Equal(2, store.Count);
        }

        [Fact]
        [DisplayName("Regression: an entry's timestamp is required by its constructor rather than left for the caller to set")]
        public void Entry_RequiresTimestampAtConstruction()
        {
            // `Entry.LastTouchedTimestamp` used to default to 0, and `GetOrAdd` wrote the real timestamp only after
            // creating the entry. A sweep landing in that gap read 0, judged the entry older than any cutoff and
            // removed a window in use. The next request of that session then got a fresh window with no history,
            // which silently reset replay protection for that session once.
            //
            // Why reflection instead of a behavior test: this race cannot be isolated cleanly by behavior. Hitting
            // it requires interleaving a sweep with an entry insertion, and a fake clock only freezes time; it does
            // not schedule threads. Using real time with a lifetime of milliseconds makes any scheduling delay longer
            // than the lifetime a **legitimate** eviction, so the test goes red even with the fix in place. A parallel
            // version was measured and went red once in three runs.
            //
            // With the timestamp carried by a constructor parameter, the invariant moves from "someone must remember
            // to stamp it at run time" to "impossible to violate at compile time". So this pins the type's shape:
            // without a parameterless constructor there is no way to leave it at 0.
            var entryType = typeof(MemoryReplayWindowStore)
                .GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.NotNull(entryType);

            var constructors = entryType!.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var only = Assert.Single(constructors);
            var parameter = Assert.Single(only.GetParameters());
            Assert.Equal(typeof(long), parameter.ParameterType);
        }

        [Fact]
        [DisplayName("Entries idle longer than the lifetime are swept so memory stays bounded")]
        public void SweepIfDue_IdleEntries_AreEvicted()
        {
            var clock = new FakeTimeProvider();
            var store = new MemoryReplayWindowStore(clock);

            for (int i = 0; i < 50; i++) { store.GetOrAdd(Guid.NewGuid()); }
            Assert.Equal(50, store.Count);

            clock.Advance(Lifetime + TimeSpan.FromTicks(1));
            store.GetOrAdd(Guid.NewGuid());   // The sweep is triggered by access, not by a background timer.

            // The clock is fake, so this can be exact: all 50 expired and only the one just added remains.
            Assert.Equal(1, store.Count);
        }

        [Fact]
        [DisplayName("Entries idle for exactly the lifetime are kept because eviction requires strictly longer")]
        public void SweepIfDue_IdleExactlyLifetime_AreKept()
        {
            var clock = new FakeTimeProvider();
            var store = new MemoryReplayWindowStore(clock);

            for (int i = 0; i < 50; i++) { store.GetOrAdd(Guid.NewGuid()); }

            // Exactly one lifetime passes: the sweep runs (the throttle has the same length), but nothing should be swept.
            clock.Advance(Lifetime);
            store.GetOrAdd(Guid.NewGuid());

            Assert.Equal(51, store.Count);
        }

        [Fact]
        [DisplayName("Throttle: no sweep runs until a full lifetime has passed since the previous sweep")]
        public void SweepIfDue_WithinThrottleWindow_DoesNotSweep()
        {
            var clock = new FakeTimeProvider();
            var store = new MemoryReplayWindowStore(clock);

            for (int i = 0; i < 50; i++) { store.GetOrAdd(Guid.NewGuid()); }

            // A full lifetime triggers one sweep and moves the throttle's starting point to now.
            // The 50 entries have been idle for exactly the lifetime and stay (another test pins that boundary).
            clock.Advance(Lifetime);
            store.GetOrAdd(Guid.NewGuid());
            Assert.Equal(51, store.Count);

            // Advance to 1 tick short of the next lifetime. The 50 entries have been idle for almost two lifetimes and
            // **are overdue for eviction**, but the previous sweep was 1 tick too recent, so this access does not sweep.
            clock.Advance(Lifetime - TimeSpan.FromTicks(1));
            store.GetOrAdd(Guid.NewGuid());
            Assert.Equal(52, store.Count);

            // Control: 1 more tick and the 50 entries disappear. So the throttle kept them in the previous step, not
            // "not yet expired", otherwise this single tick would make no difference.
            clock.Advance(TimeSpan.FromTicks(1));
            store.GetOrAdd(Guid.NewGuid());
            Assert.Equal(3, store.Count);
        }

        [Fact]
        [DisplayName("A session in continuous use is never evicted so replay protection is not silently reset")]
        public void GetOrAdd_ActiveToken_IsNeverEvicted()
        {
            var clock = new FakeTimeProvider();
            var store = new MemoryReplayWindowStore(clock);
            var active = Guid.NewGuid();

            var window = store.GetOrAdd(active);
            Assert.True(window.TryAccept(42));

            // The total time far exceeds the lifetime, but every round touches the entry. `GetOrAdd` refreshes the
            // timestamp, so it never counts as idle.
            for (int round = 0; round < 10; round++)
            {
                clock.Advance(Lifetime * 0.9);
                Assert.Same(window, store.GetOrAdd(active));
            }

            // If the window had been replaced, 42 would be accepted here, which is exactly what a silent reset of
            // replay protection looks like.
            Assert.False(store.GetOrAdd(active).TryAccept(42));
        }
    }
}
