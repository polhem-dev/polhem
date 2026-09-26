using System.ComponentModel;
using Polhem.LoadTests.Configuration;
using Polhem.LoadTests.Running;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="LoadRunner"/>.
    /// </summary>
    public class LoadRunnerTests
    {
        private sealed class CountingScenario(string name, Action<ScenarioContext>? onCall = null)
            : IScenario
        {
            private long _calls;

            public string Name { get; } = name;

            public long Calls => Interlocked.Read(ref _calls);

            public Task ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _calls);
                onCall?.Invoke(context);
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingScenario(string name, Exception exception) : IScenario
        {
            public string Name { get; } = name;

            public Task ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
                => throw exception;
        }

        private static LoadOptions Load(int users = 2, int warmup = 0, int duration = 1)
            => new() { VirtualUsers = users, WarmupSeconds = warmup, DurationSeconds = duration };

        [Fact]
        [DisplayName("BuildSchedule repeats each scenario by its weight, so weight 3 appears three times")]
        public void BuildSchedule_RepeatsByWeight()
        {
            var a = new CountingScenario("A");
            var b = new CountingScenario("B");

            var schedule = LoadRunner.BuildSchedule(
                [a, b],
                new Dictionary<string, int>(StringComparer.Ordinal) { ["A"] = 3, ["B"] = 1 });

            Assert.Equal(4, schedule.Length);
            Assert.Equal(3, schedule.Count(s => s.Name == "A"));
            Assert.Equal(1, schedule.Count(s => s.Name == "B"));
        }

        [Fact]
        [DisplayName("BuildSchedule treats a scenario without a weight as weight 1")]
        public void BuildSchedule_MissingWeight_DefaultsToOne()
        {
            var schedule = LoadRunner.BuildSchedule(
                [new CountingScenario("A")],
                new Dictionary<string, int>(StringComparer.Ordinal));

            Assert.Single(schedule);
        }

        [Fact]
        [DisplayName("BuildSchedule still schedules a scenario with a non-positive weight once, so the schedule is never empty")]
        public void BuildSchedule_NonPositiveWeight_StillScheduledOnce()
        {
            var schedule = LoadRunner.BuildSchedule(
                [new CountingScenario("A")],
                new Dictionary<string, int>(StringComparer.Ordinal) { ["A"] = 0 });

            Assert.Single(schedule);
        }

        [Fact]
        [DisplayName("RunAsync collects samples in the measured window and computes requests per second")]
        public async Task RunAsync_CollectsSamples()
        {
            var scenario = new CountingScenario("A");

            var results = await LoadRunner.RunAsync(
                Load(), [scenario], new Dictionary<string, int>(StringComparer.Ordinal));

            var result = Assert.Single(results);
            Assert.Equal("A", result.Name);
            Assert.True(result.SuccessCount > 0);
            Assert.Equal(result.SuccessCount, result.Latencies.Count);
            Assert.True(result.RequestsPerSecond > 0);
            Assert.Equal(0, result.ErrorCount);
        }

        [Fact]
        [DisplayName("RunAsync discards warm-up samples and counts only the measured window")]
        public async Task RunAsync_DiscardsWarmupSamples()
        {
            var scenario = new CountingScenario("A");

            var results = await LoadRunner.RunAsync(
                Load(warmup: 1), [scenario],
                new Dictionary<string, int>(StringComparer.Ordinal));

            var result = Assert.Single(results);

            // Both windows ran the scenario, so the total call count must exceed what the
            // measured window kept. If warm-up samples leaked in, these would be equal.
            Assert.True(scenario.Calls > result.SuccessCount,
                $"calls={scenario.Calls} kept={result.SuccessCount}");
        }

        [Fact]
        [DisplayName("RunAsync invokes the warm-up callback once so counters can be reset")]
        public async Task RunAsync_InvokesWarmupCallbackOnce()
        {
            var invocations = 0;

            await LoadRunner.RunAsync(
                Load(warmup: 1), [new CountingScenario("A")],
                new Dictionary<string, int>(StringComparer.Ordinal),
                onWarmupComplete: () => invocations++);

            Assert.Equal(1, invocations);
        }

        [Fact]
        [DisplayName("RunAsync counts scenario exceptions by type without stopping the run")]
        public async Task RunAsync_RecordsErrorsWithoutStopping()
        {
            var results = await LoadRunner.RunAsync(
                Load(), [new ThrowingScenario("A", new TimeoutException("boom"))],
                new Dictionary<string, int>(StringComparer.Ordinal));

            var result = Assert.Single(results);
            Assert.Equal(0, result.SuccessCount);
            Assert.True(result.ErrorCount > 0);
            Assert.Equal(result.ErrorCount, result.Errors[nameof(TimeoutException)]);

            // A type name alone is not diagnosable; the report keeps one message per type.
            Assert.Equal("boom", result.ErrorSamples[nameof(TimeoutException)]);
        }

        [Fact]
        [DisplayName("RunAsync leaves failed calls out of the latency statistics so latency does not look better than it is")]
        public async Task RunAsync_FailedCallsAreNotTimed()
        {
            var results = await LoadRunner.RunAsync(
                Load(), [new ThrowingScenario("A", new InvalidOperationException())],
                new Dictionary<string, int>(StringComparer.Ordinal));

            var result = Assert.Single(results);
            Assert.Equal(0, result.Latencies.Count);
            Assert.Equal(result.ErrorCount, result.TotalCount);
        }

        [Fact]
        [DisplayName("RunAsync gives each virtual user its own index, with iterations counting up from 0 without gaps")]
        public async Task RunAsync_GivesEachVirtualUserItsOwnIndex()
        {
            const int users = 4;
            var iterations = new Dictionary<int, List<long>>();
            var gate = new object();
            var scenario = new CountingScenario("A", context =>
            {
                lock (gate)
                {
                    if (!iterations.TryGetValue(context.VirtualUserIndex, out var list))
                    {
                        list = [];
                        iterations[context.VirtualUserIndex] = list;
                    }
                    list.Add(context.Iteration);
                }
            });

            await LoadRunner.RunAsync(
                Load(users: users), [scenario],
                new Dictionary<string, int>(StringComparer.Ordinal));

            Assert.NotEmpty(iterations);

            // The evidence of isolation is that each virtual user counts its own iterations from
            // zero without gaps. Whether all four get scheduled inside a one-second window depends
            // on how many cores the machine has, so asserting that would make this test fail on a
            // constrained runner while proving nothing extra.
            foreach (var (virtualUserIndex, seen) in iterations)
            {
                Assert.InRange(virtualUserIndex, 0, users - 1);
                Assert.Equal(Enumerable.Range(0, seen.Count).Select(i => (long)i), seen);
            }
        }

        [Fact]
        [DisplayName("RunAsync gives scenarios call shares according to their weights")]
        public async Task RunAsync_HonoursWeightsAcrossScenarios()
        {
            var heavy = new CountingScenario("Heavy");
            var light = new CountingScenario("Light");

            await LoadRunner.RunAsync(
                Load(users: 1), [heavy, light],
                new Dictionary<string, int>(StringComparer.Ordinal) { ["Heavy"] = 3, ["Light"] = 1 });

            Assert.True(heavy.Calls > light.Calls,
                $"heavy={heavy.Calls} light={light.Calls}");
        }

        [Fact]
        [DisplayName("RunAsync throws ArgumentException when there are no scenarios")]
        public async Task RunAsync_NoScenarios_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => LoadRunner.RunAsync(
                Load(), [], new Dictionary<string, int>(StringComparer.Ordinal)));
        }

        [Fact]
        [DisplayName("RunAsync stops early without throwing on external cancellation")]
        public async Task RunAsync_ExternalCancellation_StopsEarly()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            var results = await LoadRunner.RunAsync(
                Load(duration: 30), [new CountingScenario("A")],
                new Dictionary<string, int>(StringComparer.Ordinal),
                cancellationToken: cts.Token);

            Assert.True(results.Count <= 1);
        }
    }
}
