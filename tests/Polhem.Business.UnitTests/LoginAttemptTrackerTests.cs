using System.ComponentModel;
using Polhem.Business.Security;

namespace Polhem.Business.UnitTests
{
    /// <summary>
    /// Brute-force protection tests for LoginAttemptTracker.
    /// </summary>
    public class LoginAttemptTrackerTests
    {
        [Fact]
        [DisplayName("A new account is not locked out")]
        public void IsLockedOut_NewUser_ReturnsFalse()
        {
            var tracker = new LoginAttemptTracker();
            Assert.False(tracker.IsLockedOut("user01"));
        }

        [Fact]
        [DisplayName("An account below the maximum failed attempts is not locked out")]
        public void IsLockedOut_BelowMaxAttempts_ReturnsFalse()
        {
            var tracker = new LoginAttemptTracker(5, TimeSpan.FromMinutes(15));

            for (int i = 0; i < 4; i++)
                tracker.RecordFailure("user01");

            Assert.False(tracker.IsLockedOut("user01"));
        }

        [Fact]
        [DisplayName("An account that reaches the maximum failed attempts is locked out")]
        public void IsLockedOut_ReachMaxAttempts_ReturnsTrue()
        {
            var tracker = new LoginAttemptTracker(5, TimeSpan.FromMinutes(15));

            for (int i = 0; i < 5; i++)
                tracker.RecordFailure("user01");

            Assert.True(tracker.IsLockedOut("user01"));
        }

        [Fact]
        [DisplayName("The account unlocks automatically after the lockout period")]
        public void IsLockedOut_AfterLockoutExpires_ReturnsFalse()
        {
            // A fake clock that can be advanced replaces the real wall clock. The old version used a 50ms lockout window and `Task.Delay`, which under
            // scheduling pressure on 2-core CI could expire before the should-be-locked assertion and go flaky. Advancing logical time removes that risk.
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15), clock);

            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("user01");

            Assert.True(tracker.IsLockedOut("user01"));

            clock.Advance(TimeSpan.FromMinutes(16));

            Assert.False(tracker.IsLockedOut("user01"));
        }

        /// <summary>
        /// A clock advanced by hand, for verifying lockout expiry in logical time.
        /// </summary>
        private sealed class AdvanceableTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow;

            public AdvanceableTimeProvider(DateTimeOffset start) => _utcNow = start;

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
        }

        [Fact]
        [DisplayName("A successful login resets the failure count")]
        public void Reset_AfterFailures_ClearsLockout()
        {
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15));

            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("user01");

            Assert.True(tracker.IsLockedOut("user01"));

            tracker.Reset("user01");
            Assert.False(tracker.IsLockedOut("user01"));
        }

        [Fact]
        [DisplayName("Failure counts of different accounts are independent")]
        public void RecordFailure_DifferentUsers_IndependentTracking()
        {
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15));

            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("user01");

            tracker.RecordFailure("user02");

            Assert.True(tracker.IsLockedOut("user01"));
            Assert.False(tracker.IsLockedOut("user02"));
        }

        [Fact]
        [DisplayName("After Reset the count starts over, and failures must accumulate again before a lockout")]
        public void Reset_ThenFailAgain_RequiresFullCountToLock()
        {
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15));

            // Fail 2 times, then reset
            tracker.RecordFailure("user01");
            tracker.RecordFailure("user01");
            tracker.Reset("user01");

            // Fail 2 more times — should NOT be locked (count was reset)
            tracker.RecordFailure("user01");
            tracker.RecordFailure("user01");
            Assert.False(tracker.IsLockedOut("user01"));

            // Third failure triggers lockout again
            tracker.RecordFailure("user01");
            Assert.True(tracker.IsLockedOut("user01"));
        }

        [Fact]
        [DisplayName("Account names are case-insensitive")]
        public void RecordFailure_CaseInsensitive_TracksAsSameUser()
        {
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15));

            tracker.RecordFailure("User01");
            tracker.RecordFailure("USER01");
            tracker.RecordFailure("user01");

            Assert.True(tracker.IsLockedOut("user01"));
        }

        [Fact]
        [DisplayName("An empty or null userId does not throw")]
        public void RecordFailure_NullOrEmpty_DoesNotThrow()
        {
            var tracker = new LoginAttemptTracker();

            tracker.RecordFailure(null!);
            tracker.RecordFailure(string.Empty);
            Assert.False(tracker.IsLockedOut(null!));
            Assert.False(tracker.IsLockedOut(string.Empty));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [DisplayName("MaxFailedAttempts cannot be zero or negative")]
        public void Constructor_InvalidMaxAttempts_ThrowsArgumentOutOfRangeException(int maxAttempts)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LoginAttemptTracker(maxAttempts, TimeSpan.FromMinutes(15)));
        }

        [Fact]
        [DisplayName("LockoutDuration cannot be zero or negative")]
        public void Constructor_InvalidDuration_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LoginAttemptTracker(5, TimeSpan.Zero));
        }

        [Fact]
        [DisplayName("Another RecordFailure during the lockout keeps the existing locked state")]
        public void RecordFailure_DuringLockout_KeepsLockedState()
        {
            // A 10-minute lockout keeps the test inside the lockout period.
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(10));

            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("user01");
            Assert.True(tracker.IsLockedOut("user01"));

            // Triggers the early-return branch in `IncrementFailure` where `LockedUntilUtc` is still valid.
            tracker.RecordFailure("user01");
            tracker.RecordFailure("user01");

            Assert.True(tracker.IsLockedOut("user01"));
        }
        [Fact]
        [DisplayName("Failed accounts that never repeat do not accumulate without bound, and are cleared after they expire")]
        public void RecordFailure_DistinctUsersNeverRepeated_EntriesExpire()
        {
            // The attacker's shape: a different user ID every time, so lazy cleanup that runs when the same key comes back
            // never triggers. The old implementation grew without bound here.
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(5, TimeSpan.FromMinutes(15), clock)
            {
                MaxTrackedAccounts = 100
            };

            for (int i = 0; i < 100; i++)
                tracker.RecordFailure($"attacker-{i}");

            // One more attempt after the window: the sweep must clear all 100 earlier entries, otherwise the capacity cap would block the entry below.
            clock.Advance(TimeSpan.FromMinutes(16));
            tracker.RecordFailure("victim");

            for (int i = 0; i < 5; i++)
                tracker.RecordFailure("victim");

            Assert.True(tracker.IsLockedOut("victim"));
        }

        [Fact]
        [DisplayName("A new account beyond the cap is still tracked and locks out, evicting the oldest entry instead")]
        public void RecordFailure_BeyondCap_NewAccountStillLocksOut()
        {
            // Dropping new accounts at the cap used to let one burst of random user ids switch lockout off for every
            // account not already tracked.
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15), clock)
            {
                MaxTrackedAccounts = 10
            };

            for (int i = 0; i < 10; i++)
            {
                tracker.RecordFailure($"filler-{i}");
                clock.Advance(TimeSpan.FromSeconds(1));
            }

            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("late-comer");

            Assert.True(tracker.IsLockedOut("late-comer"));
        }

        [Fact]
        [DisplayName("At the cap, an active lockout is kept while unlocked entries are evicted")]
        public void RecordFailure_BeyondCap_KeepsLockedEntries()
        {
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15), clock)
            {
                MaxTrackedAccounts = 5
            };

            // The locked account is the oldest entry, so an eviction by age alone would free it first.
            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("locked");
            clock.Advance(TimeSpan.FromSeconds(1));

            for (int i = 0; i < 20; i++)
            {
                tracker.RecordFailure($"decoy-{i}");
                clock.Advance(TimeSpan.FromSeconds(1));
            }

            Assert.True(tracker.IsLockedOut("locked"));
        }

        [Fact]
        [DisplayName("An account already being tracked is not affected by the capacity cap and still locks out")]
        public void RecordFailure_ExistingAccountAtCap_StillLocksOut()
        {
            // The cap only blocks new accounts. If it blocked existing ones too, flooding the capacity would become a way to switch off one account's lockout.
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15), clock)
            {
                MaxTrackedAccounts = 10
            };

            tracker.RecordFailure("victim");
            for (int i = 0; i < 9; i++)
                tracker.RecordFailure($"filler-{i}");

            tracker.RecordFailure("victim");
            tracker.RecordFailure("victim");

            Assert.True(tracker.IsLockedOut("victim"));
        }

        [Fact]
        [DisplayName("Failures are counted within a window, so sporadic failures across windows do not accumulate into a lockout")]
        public void RecordFailure_SpreadAcrossWindows_DoesNotAccumulate()
        {
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15), clock);

            for (int i = 0; i < 5; i++)
            {
                tracker.RecordFailure("typo-user");
                Assert.False(tracker.IsLockedOut("typo-user"));
                clock.Advance(TimeSpan.FromMinutes(16));
            }
        }

        [Fact]
        [DisplayName("Continued failures during the lockout do not extend it")]
        public void RecordFailure_WhileLockedOut_DoesNotExtendLockout()
        {
            var clock = new AdvanceableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var tracker = new LoginAttemptTracker(3, TimeSpan.FromMinutes(15), clock);

            for (int i = 0; i < 3; i++)
                tracker.RecordFailure("user01");
            Assert.True(tracker.IsLockedOut("user01"));

            // If failures during the lockout extended it, an attacker could keep an account locked forever.
            clock.Advance(TimeSpan.FromMinutes(14));
            tracker.RecordFailure("user01");

            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.False(tracker.IsLockedOut("user01"));
        }

    }
}
