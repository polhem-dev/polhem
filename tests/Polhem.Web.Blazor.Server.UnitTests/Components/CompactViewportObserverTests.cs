using System.ComponentModel;
using System.Reflection;
using Bunit;
using Microsoft.JSInterop;
using Polhem.Tests.Shared;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// Drives <see cref="CompactViewportObserver"/> through bUnit's strict JavaScript interop: what the module's
    /// <c>start</c> and <c>update</c> return, and what its change listener reports, stand in for the browser.
    /// </summary>
    public class CompactViewportObserverTests : BunitContext
    {
        private readonly List<bool> _reports = [];

        private IRenderedComponent<CompactViewportObserver> RenderObserver(double threshold = CompactViewportObserver.DefaultCompactWidthThreshold)
            => Render<CompactViewportObserver>(p => p
                .Add(c => c.CompactWidthThreshold, threshold)
                .Add(c => c.CompactChanged, compact => _reports.Add(compact)));

        // Calls the method the module's change listener calls, on the object the observer passed to `start`.
        private static async Task RaiseChangeAsync(BunitJSModuleInterop module, bool compact)
        {
            var reference = module.Invocations["start"].Single().Arguments[0]!;
            var target = reference.GetType().GetProperty("Value")!.GetValue(reference)!;
            var method = target.GetType().GetMethod("OnCompactChanged", BindingFlags.Public | BindingFlags.Instance)!;
            await (Task)method.Invoke(target, [compact])!;
        }

        [Fact]
        [DisplayName("CompactViewportObserver starts watching with its threshold after the first render")]
        public void FirstRender_StartsWatchingWithThreshold()
        {
            var module = CompactViewportModule.Setup(JSInterop);

            RenderObserver(threshold: 720);

            var start = module.Invocations["start"].Single();
            Assert.Equal(720d, start.Arguments[2]);
        }

        [Fact]
        [DisplayName("CompactViewportObserver reports a viewport that is narrow when the module starts")]
        public void Start_NarrowViewport_ReportsCompact()
        {
            CompactViewportModule.Setup(JSInterop, compact: true);

            RenderObserver();

            Assert.Equal([true], _reports);
        }

        [Fact]
        [DisplayName("CompactViewportObserver reports nothing for a viewport that is wide when the module starts")]
        public void Start_WideViewport_ReportsNothing()
        {
            CompactViewportModule.Setup(JSInterop, compact: false);

            RenderObserver();

            Assert.Empty(_reports);
        }

        [Fact]
        [DisplayName("CompactViewportObserver reports each crossing the module raises, and drops a repeated value")]
        public async Task Change_Crossings_ReportedOnce()
        {
            var module = CompactViewportModule.Setup(JSInterop);
            var cut = RenderObserver();

            await cut.InvokeAsync(() => RaiseChangeAsync(module, compact: true));
            await cut.InvokeAsync(() => RaiseChangeAsync(module, compact: true));
            await cut.InvokeAsync(() => RaiseChangeAsync(module, compact: false));

            Assert.Equal([true, false], _reports);
        }

        [Fact]
        [DisplayName("CompactViewportObserver re-queries the module with a changed threshold and reports the result")]
        public void ThresholdChanged_UpdatesModule()
        {
            var module = CompactViewportModule.Setup(JSInterop);
            module.Setup<bool>("update", _ => true).SetResult(true);
            var cut = RenderObserver();

            cut.Render(p => p.Add(c => c.CompactWidthThreshold, 900d));

            Assert.Equal(900d, module.Invocations["update"].Single().Arguments[1]);
            Assert.Equal([true], _reports);
        }

        [Fact]
        [DisplayName("CompactViewportObserver removes its listener when disposed")]
        public async Task Dispose_StopsWatching()
        {
            var module = CompactViewportModule.Setup(JSInterop);
            RenderObserver();

            await DisposeComponentsAsync();

            var start = module.Invocations["start"].Single();
            Assert.Equal(start.Arguments[1], module.Invocations["stop"].Single().Arguments[0]);
        }

        [Fact]
        [DisplayName("CompactViewportObserver disposes quietly when the circuit has already disconnected")]
        public async Task Dispose_Disconnected_DoesNotThrow()
        {
            var module = JSInterop.SetupModule(CompactViewportModule.Path);
            module.Setup<bool>("start", _ => true).SetResult(false);
            module.SetupVoid("stop", _ => true).SetException(new JSDisconnectedException("The circuit is gone."));
            RenderObserver();

            var exception = await Record.ExceptionAsync(() => DisposeComponentsAsync());

            Assert.Null(exception);
        }

        [Fact]
        [DisplayName("The module path CompactViewportObserver imports names the JavaScript file shipped next to it")]
        public void ModulePath_NamesShippedFile()
        {
            const string prefix = "./_content/Polhem.Web.Blazor.Server/";
            Assert.StartsWith(prefix, CompactViewportModule.Path, StringComparison.Ordinal);

            var relative = CompactViewportModule.Path[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var file = Path.Combine(RepoRoot.Find(), "src", "Polhem.Web.Blazor.Server", relative);
            Assert.True(File.Exists(file), $"{CompactViewportModule.Path} has no file at {file}.");
        }
    }
}
