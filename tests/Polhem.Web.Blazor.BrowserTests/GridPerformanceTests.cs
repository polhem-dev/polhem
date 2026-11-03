using System.ComponentModel;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Polhem.Web.Blazor.BrowserTests
{
    /// <summary>
    /// Keeps the first load of a grid being edited from growing, on 200 rows by 15 columns. Memory depends on the runtime
    /// too much to gate here, so it is only reported.
    /// </summary>
    public abstract class GridPerformanceTests
    {
        private readonly BrowserSiteFixture _fixture;
        private readonly ITestOutputHelper _output;

        /// <summary>Gets the limit of the first load, which depends on the UI framework.</summary>
        protected abstract long FirstLoadLimitBytes { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="GridPerformanceTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="output">Where the measurements go.</param>
        protected GridPerformanceTests(BrowserSiteFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [BrowserFact]
        [DisplayName("The first load of 200 rows by 15 columns stays within the circuit traffic limit")]
        public async Task FirstLoad_LargeGrid_StaysWithinTheLimit()
        {
            var before = GC.GetTotalMemory(forceFullCollection: true);
            var received = 0L;
            var page = await _fixture.Browser.NewPageAsync();
            page.WebSocket += (_, socket) => socket.FrameReceived += (_, frame) =>
                Interlocked.Add(ref received, frame.Binary?.Length ?? frame.Text?.Length ?? 0);

            await page.GotoAsync(_fixture.Address + "/perf");
            await Assertions.Expect(page.Locator("tr.polhem-grid__row")).ToHaveCountAsync(200);
            // The rows are prerendered, so they show before the circuit sends anything. What is measured is the circuit's
            // traffic, which Playwright's network idle does not count; waiting for it timed out in CI on 2026-11-01 and
            // 2026-11-03 with no request open on the server.
            await page.Locator("body[data-interactive]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            Assert.True(await WaitForQuietAsync(page, () => Interlocked.Read(ref received)),
                $"The circuit was still sending after {s_quietDeadline.TotalSeconds} seconds.");
            var nodes = await page.EvaluateAsync<int>("document.querySelectorAll('*').length");
            var after = GC.GetTotalMemory(forceFullCollection: true);
            _output.WriteLine($"circuit traffic {received:N0} bytes, {nodes:N0} elements, managed memory +{(after - before) / 1_000_000.0:N1} MB");

            Assert.InRange(Interlocked.Read(ref received), 1, FirstLoadLimitBytes);
        }

        private static readonly TimeSpan s_quietPeriod = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan s_quietDeadline = TimeSpan.FromSeconds(20);

        // True once the count has not grown for a whole quiet period, false if that does not happen before the deadline.
        private static async Task<bool> WaitForQuietAsync(IPage page, Func<long> count)
        {
            var deadline = DateTime.UtcNow + s_quietDeadline;
            var last = count();
            var quietSince = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                await page.WaitForTimeoutAsync(100);
                var now = count();
                if (now != last)
                {
                    last = now;
                    quietSince = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - quietSince >= s_quietPeriod)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
