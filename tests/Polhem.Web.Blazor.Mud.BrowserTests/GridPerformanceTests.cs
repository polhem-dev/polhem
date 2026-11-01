using System.ComponentModel;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Polhem.Web.Blazor.Mud.BrowserTests
{
    /// <summary>
    /// Keeps the first load of a grid being edited from growing, on 200 rows by 15 columns. Measured on 2026-11-01 with the
    /// same page and method: the MudBlazor grid sends 1.76 MB and holds 30 to 36 MB, the HTML grid 1.53 MB and 53 to 59 MB.
    /// The maintainer accepted the extra traffic for the memory. Memory depends on the runtime too much to gate here, so it
    /// is only reported.
    /// </summary>
    public class GridPerformanceTests : IClassFixture<GridBrowserFixture>
    {
        private const long FirstLoadLimitBytes = 1_900_000;

        private readonly GridBrowserFixture _fixture;
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of <see cref="GridPerformanceTests"/>.
        /// </summary>
        /// <param name="fixture">The site and the browser.</param>
        /// <param name="output">Where the measurements go.</param>
        public GridPerformanceTests(GridBrowserFixture fixture, ITestOutputHelper output)
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
