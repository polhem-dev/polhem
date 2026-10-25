using System.Reflection;
using Bunit;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.UnitTests.Components
{
    /// <summary>
    /// The JavaScript module of <see cref="CompactViewportObserver"/>, as the tests set it up in bUnit's
    /// strict JavaScript interop.
    /// </summary>
    internal static class CompactViewportModule
    {
        /// <summary>
        /// Gets the path the observer imports, read from the component so the tests follow it.
        /// </summary>
        public static string Path { get; } = (string)typeof(CompactViewportObserver)
            .GetField("ModulePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        /// <summary>
        /// Sets up the module so that <c>start</c> reports <paramref name="compact"/>, and <c>update</c> and
        /// <c>stop</c> succeed.
        /// </summary>
        public static BunitJSModuleInterop Setup(BunitJSInterop jsInterop, bool compact = false)
        {
            var module = jsInterop.SetupModule(Path);
            module.Setup<bool>("start", _ => true).SetResult(compact);
            module.SetupVoid("stop", _ => true).SetVoidResult();
            return module;
        }
    }
}
