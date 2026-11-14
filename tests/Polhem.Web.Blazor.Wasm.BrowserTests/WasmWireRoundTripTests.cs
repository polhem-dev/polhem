using System.ComponentModel;
using System.Text.Json;
using Polhem.Api.Client;
using Polhem.Web.Blazor.BrowserTests;

namespace Polhem.Web.Blazor.Wasm.BrowserTests
{
    /// <summary>
    /// The calls of a Blazor WebAssembly application, made on the WebAssembly runtime from a trimmed publish, carry every
    /// value the way they do on the desktop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Polhem.Web.Blazor.Wasm</c> calls the backend through <see cref="PolhemApiClient.CreateRemote"/>, and nothing
    /// else runs that path on the browser's runtime: the AOT gate in CI covers the desktop and Apple runtimes only. The
    /// browser interprets with dynamic code, so it is expected to behave like the desktop. The iOS wire broke once on
    /// that same kind of expectation, so this test measures it.
    /// </para>
    /// <para>
    /// The page and this test run one <see cref="WireScenario"/>: each scenario saves three rows with a value of every
    /// data type and a detail table, reads them back, and lists them through a filter on every type. The browser's
    /// report has to show every step completing, every value read back as it was saved, every filter returning its
    /// rows, and to equal the report of the same scenario run here on the desktop runtime.
    /// </para>
    /// </remarks>
    public sealed class WasmWireRoundTripTests : IClassFixture<WireSiteFixture>
    {
        private readonly WireSiteFixture _site;

        public WasmWireRoundTripTests(WireSiteFixture site) { _site = site; }

        [BrowserTheory]
        [InlineData(WireSample.MessagePack)]
        [InlineData(WireSample.Json)]
        [InlineData(WireSample.Plain)]
        [DisplayName("On the WebAssembly runtime every value saved, read back and filtered on arrives as it does on the desktop")]
        public async Task Scenario_OnWebAssembly_MatchesTheDesktop(string scenario)
        {
            var browser = await RunInBrowserAsync(scenario);
            var desktop = await WireScenario.RunAsync(PolhemApiClient.CreateRemote($"{_site.Address}/api", WireSample.ApiKey), scenario, WireSample.DesktopRuntime);

            Assert.All(desktop, step => Assert.True(step.Ok, $"desktop {step.Name}: {step.Error}"));
            Assert.All(browser, step => Assert.True(step.Ok, $"{step.Name}: {step.Error}"));
            Assert.Equal(desktop.Select(step => step.Name), browser.Select(step => step.Name));
            foreach (var row in WireSample.Rows)
            {
                var saved = browser.Single(step => step.Name == $"save {row}").Values;
                var read = browser.Single(step => step.Name == $"read back {row}").Values;
                Assert.All(saved, pair => Assert.Equal(pair.Value, read[pair.Key]));
                Assert.Equal("assigned", read[WireSample.AutoIncrementField]);
                Assert.EndsWith(":recent", read[WireSample.ServerTimeField], StringComparison.Ordinal);
                Assert.Equal(
                    $"{WireSample.Format(1)}|{WireSample.Format(1.5m)}|{WireSample.Format($"{row}1")};" +
                    $"{WireSample.Format(2)}|{WireSample.Format(3.0m)}|{WireSample.Format($"{row}2")}",
                    read["lines"]);
            }
            Assert.Equal(
                string.Join("\n", WireSample.Filters.Select(filter => $"{filter.Name}: {string.Join(",", filter.Expected)}")),
                string.Join("\n", WireSample.Filters.Select(filter =>
                    $"{filter.Name}: {browser.Single(step => step.Name == $"list {filter.Name}").Values["rows"]}")));

            Assert.Equal(Describe(desktop), Describe(browser));
        }

        private async Task<List<WireStep>> RunInBrowserAsync(string scenario)
        {
            var page = await _site.Browser.NewPageAsync();
            try
            {
                await page.GotoAsync($"{_site.Address}/?{WireSample.ScenarioParameter}={scenario}");
                var result = page.Locator("#result[data-done='true']");
                await result.WaitForAsync(new() { Timeout = 120_000 });
                return JsonSerializer.Deserialize<List<WireStep>>(await result.InnerTextAsync())!;
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        // Each run saves and lists only rows that start with its own runtime and scenario, so the two reports can be
        // compared as text.
        private static string Describe(List<WireStep> steps)
            => string.Join("\n", steps.Select(step =>
                $"{step.Name} ok={step.Ok} {step.Error} {string.Join(" ", step.Values.Select(pair => $"{pair.Key}={pair.Value}"))}"));
    }
}
