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
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var nodes = await page.EvaluateAsync<int>("document.querySelectorAll('*').length");
            var after = GC.GetTotalMemory(forceFullCollection: true);
            _output.WriteLine($"circuit traffic {received:N0} bytes, {nodes:N0} elements, managed memory +{(after - before) / 1_000_000.0:N1} MB");

            Assert.InRange(Interlocked.Read(ref received), 1, FirstLoadLimitBytes);
        }
    }
}
